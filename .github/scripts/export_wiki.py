"""Export repository documentation to a separate GitHub-wiki working folder."""
import argparse
import hashlib
import json
import re
from pathlib import Path
from urllib.parse import quote, unquote, urlsplit

MARKER = ".nxfileviewer-wiki-export.json"
GERMAN_SOURCE = ".nxfileviewer-wiki-de.json"


def collect(root):
    pages = {root / "README.md": "Home"}
    for file in sorted((root / "docs").glob("*.md")):
        pages[file] = "Documentation" if file.name == "README.md" else file.stem
    pages.update({
        root / "src/config/README.md": "Configuration",
        root / "src/config/Update-sources.md": "Update-sources",
        root / ".github/Download-badge.md": "Download-badge",
        root / "fw/README.md": "Firmware-reference-maintenance",
        root / "docs/licenses/README.md": "License-inventory",
    })
    if len(set(pages.values())) != len(pages):
        raise ValueError("Wiki page-name collision")
    for source in pages:
        if not source.is_file():
            raise FileNotFoundError(source)
    return pages


def convert(text, source, root, pages, repository, ref):
    wiki = f"https://github.com/{repository}/wiki/"
    blob = f"https://github.com/{repository}/blob/{quote(ref, safe='')}/"
    raw = f"https://raw.githubusercontent.com/{repository}/{quote(ref, safe='')}/"

    def link(match):
        target = match[1]
        if urlsplit(target).scheme or target.startswith(("#", "//")):
            return match[0]
        path, separator, fragment = target.partition("#")
        destination = (source.parent / unquote(path)).resolve()
        if not destination.is_relative_to(root) or not destination.exists():
            raise ValueError(f"Broken/outside-repository link in {source.name}: {target}")
        if destination in pages:
            result = wiki + quote(pages[destination], safe="-_")
        else:
            relative = destination.relative_to(root).as_posix()
            base = raw if destination.suffix.lower() in {".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp", ".ico"} else blob
            result = base + quote(relative, safe="/")
        return "](" + result + (separator + fragment if separator else "") + ")"

    result = []
    fence = None
    for line in text.splitlines(keepends=True):
        stripped = line.lstrip()
        marker = re.match(r"(" + chr(96) + r"{3,}|~{3,})", stripped)
        if marker:
            if fence is None:
                fence = marker[1][0]
            elif marker[1][0] == fence:
                fence = None
            result.append(line)
        elif fence is None:
            result.append(re.sub(r"\]\(([^)\n]+)\)", link, line))
        else:
            result.append(line)
    return "".join(result)


def export(root, output, repository, ref="master", overwrite=False, german_source=None):
    root, output = Path(root).resolve(), Path(output).resolve()
    if ".git" in output.parts:
        raise ValueError("Wiki output cannot be inside .git")
    if output.is_relative_to(root):
        raise ValueError("Wiki output must be outside the application repository")
    if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository):
        raise ValueError("Expected owner/repository")
    pages = collect(root)
    translation_path = Path(german_source).resolve() if german_source else output / GERMAN_SOURCE
    translations = {}
    if translation_path.exists():
        data = json.loads(translation_path.read_text(encoding="utf-8-sig"))
        if not isinstance(data, dict) or data.get("schemaVersion") != 1 or not isinstance(data.get("pages"), dict):
            raise ValueError("Invalid German wiki translation source")
        translations = data["pages"]
        for page, entry in translations.items():
            if page not in pages.values() or not isinstance(entry, dict) or any(
                not isinstance(entry.get(key), str) or not entry[key].strip() for key in ("title", "content")
            ):
                raise ValueError(f"Invalid German wiki page: {page}")
    elif (output / MARKER).exists():
        previous = json.loads((output / MARKER).read_text(encoding="utf-8"))
        if isinstance(previous, list) and any(isinstance(name, str) and name.startswith("DE-") for name in previous):
            raise ValueError("German translation source is missing; preserving existing German pages")
    conflicts = set(pages.values()) & {"DE-" + page for page in translations}
    if conflicts:
        raise ValueError(f"German page-name collision: {sorted(conflicts)}")
    generated, titles, german_titles = {}, {}, {}
    wiki = f"https://github.com/{repository}/wiki/"
    german_pages = {source: "DE-" + page if page in translations else page for source, page in pages.items()}
    for source, page in pages.items():
        original = source.read_text(encoding="utf-8-sig")
        heading = re.search(r"(?m)^# (.+)$", original)
        titles[page] = heading[1] if heading else page.replace("-", " ")
        origin = source.relative_to(root).as_posix()
        switch = f"[English]({wiki}{quote(page, safe='-_')}) | [Deutsch]({wiki}DE-{quote(page, safe='-_')})\n\n" if page in translations else ""
        generated[page + ".md"] = (
            f"<!-- Generated from {origin}; edit the source repository and export again. -->\n\n"
            + switch + convert(original, source, root, pages, repository, ref).rstrip() + "\n"
        )
        if page in translations:
            entry = translations[page]
            german_titles[page] = entry["title"]
            content = convert(entry["content"], source, root, german_pages, repository, ref)
            if entry.get("sourceSha256") and entry["sourceSha256"] != hashlib.sha256(original.encode("utf-8")).hexdigest():
                content = "> Hinweis: Die englische Quelle wurde seit der letzten deutschen Überarbeitung geändert. Bitte auch die englische Fassung prüfen.\n\n" + content
            generated["DE-" + page + ".md"] = (
                f"<!-- German wiki text: edit {GERMAN_SOURCE} in the wiki repository and export again. -->\n\n"
                + switch + content.rstrip() + "\n"
            )
    order = ["Home", "Documentation"] + sorted(set(titles) - {"Home", "Documentation"}, key=str.casefold)
    generated["_Sidebar.md"] = "# Documentation\n\n"
    if translations:
        german_start = "Home" if "Home" in translations else next(iter(translations))
        generated["_Sidebar.md"] += f"[English]({wiki}Home) | [Deutsch]({wiki}DE-{quote(german_start, safe='-_')})\n\n## English\n\n"
    generated["_Sidebar.md"] += "\n".join(
        f"- [{titles[page]}]({wiki}{quote(page, safe='-_')})" for page in order) + "\n"
    if translations:
        generated["_Sidebar.md"] += "\n## Deutsch\n\n" + "\n".join(
            f"- [{german_titles[page]}]({wiki}DE-{quote(page, safe='-_')})"
            for page in order if page in german_titles) + "\n"
    generated["_Footer.md"] = (
        f"Generated from the [NxFileViewer documentation](https://github.com/{repository}/tree/{quote(ref, safe='')}/docs). "
        "English sources are maintained in the application repository; German translations are maintained only in the wiki repository.\n"
    )
    marker = output / MARKER
    if marker.is_symlink():
        raise ValueError("Refusing linked ownership manifest")
    managed = json.loads(marker.read_text(encoding="utf-8")) if marker.exists() else []
    if not isinstance(managed, list) or any(
        not isinstance(name, str) or Path(name).name != name or not name.endswith(".md")
        or name.startswith(".") or "/" in name or "\\" in name for name in managed
    ):
        raise ValueError("Invalid wiki export ownership manifest")
    # Validate the complete plan before changing any output files.
    for name, content in generated.items():
        target = output / name
        if target.is_symlink():
            raise ValueError(f"Refusing linked output: {target}")
        if target.exists() and name not in managed and not overwrite and target.read_text(encoding="utf-8") != content:
            raise FileExistsError(f"Existing unmanaged wiki page: {target}; use --overwrite to replace generated page names")
    for name in set(managed) - set(generated):
        if (output / name).is_symlink():
            raise ValueError("Refusing linked stale output")
    output.mkdir(parents=True, exist_ok=True)
    for name, content in generated.items():
        target = output / name
        if not target.exists() or target.read_text(encoding="utf-8") != content:
            target.write_text(content, encoding="utf-8", newline="\n")
    for name in set(managed) - set(generated):
        (output / name).unlink(missing_ok=True)
    marker.write_text(json.dumps(sorted(generated), indent=2) + "\n", encoding="utf-8")
    return len(generated)


if __name__ == "__main__":
    root = Path(__file__).resolve().parents[2]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=root.with_name(root.name + ".wiki"))
    parser.add_argument("--repository", default="sandmaennchen5/fork_NxFileViewer")
    parser.add_argument("--ref", default="master")
    parser.add_argument("--overwrite", action="store_true", help="Replace colliding generated page names, preserving other wiki pages")
    parser.add_argument("--german-source", type=Path, help="Optional German wiki JSON; defaults to the output folder")
    args = parser.parse_args()
    count = export(root, args.output, args.repository, args.ref, args.overwrite, args.german_source)
    print(f"Exported {count} wiki pages to {args.output.resolve()}")
    print("No git commands or pushes were performed. Existing .git and custom pages are preserved.")
