import tempfile
import unittest
from pathlib import Path
import export_wiki as wiki


class WikiExportTests(unittest.TestCase):
    def fixture(self, directory):
        root = Path(directory) / "project"
        for name, text in {
            "README.md": "# Project\n\n[Guide](docs/Guide.md#topic)\n![Start](screenshots/A%20B.png)\n",
            "docs/README.md": "# Documentation\n",
            "docs/Guide.md": "# Guide\n\n## Topic\n\n[Config](../src/config/README.md)\n",
            "src/config/README.md": "# Configuration\n",
            "src/config/Update-sources.md": "# Sources\n",
            ".github/Download-badge.md": "# Badge\n",
            "fw/README.md": "# Firmware maintenance\n",
            "docs/licenses/README.md": "# License maintenance\n",
            "screenshots/A B.png": "image fixture",
        }.items():
            path = root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(text, encoding="utf-8")
        return root.resolve(), (Path(directory) / "project.wiki").resolve()

    def test_links_images_and_headings(self):
        with tempfile.TemporaryDirectory() as temp:
            root, output = self.fixture(temp)
            count = wiki.export(root, output, "owner/project")
            self.assertGreater(count, 5)
            home = (output / "Home.md").read_text(encoding="utf-8")
            self.assertIn("https://github.com/owner/project/wiki/Guide#topic", home)
            self.assertIn("https://raw.githubusercontent.com/owner/project/master/screenshots/A%20B.png", home)
            self.assertIn("Configuration", (output / "_Sidebar.md").read_text())
            self.assertIn("/wiki/Configuration", (output / "Guide.md").read_text())

    def test_nested_badge_link_and_code_examples(self):
        with tempfile.TemporaryDirectory() as temp:
            root, _ = self.fixture(temp)
            ticks = chr(96) * 3
            text = "[![Badge](https://example.test/image)](.github/Download-badge.md)\n"
            text += ticks + "\n[Example](does-not-exist.md)\n" + ticks + "\n"
            converted = wiki.convert(text, root / "README.md", root, wiki.collect(root), "owner/project", "master")
            self.assertIn("](https://github.com/owner/project/wiki/Download-badge)", converted)
            self.assertIn("[Example](does-not-exist.md)", converted)

    def test_repeat_export_updates_owned_pages_and_preserves_git_and_custom_pages(self):
        with tempfile.TemporaryDirectory() as temp:
            root, output = self.fixture(temp)
            obsolete = root / "docs/Obsolete.md"
            obsolete.write_text("# Obsolete\n")
            wiki.export(root, output, "owner/project")
            (output / ".git").mkdir()
            (output / ".git/HEAD").write_text("preserve")
            (output / "Custom.md").write_text("custom")
            obsolete.unlink()
            (root / "docs/Guide.md").write_text("# Guide\n\n## Topic\n\nUpdated\n")
            wiki.export(root, output, "owner/project")
            self.assertIn("Updated", (output / "Guide.md").read_text())
            self.assertFalse((output / "Obsolete.md").exists())
            self.assertEqual("preserve", (output / ".git/HEAD").read_text())
            self.assertEqual("custom", (output / "Custom.md").read_text())

    def test_first_export_requires_consent_for_existing_page_names(self):
        with tempfile.TemporaryDirectory() as temp:
            root, output = self.fixture(temp)
            output.mkdir()
            (output / "Home.md").write_text("original home")
            with self.assertRaises(FileExistsError):
                wiki.export(root, output, "owner/project")
            self.assertEqual("original home", (output / "Home.md").read_text())
            wiki.export(root, output, "owner/project", overwrite=True)
            self.assertIn("# Project", (output / "Home.md").read_text())

    def test_export_cannot_target_source_repository_or_git_metadata(self):
        with tempfile.TemporaryDirectory() as temp:
            root, output = self.fixture(temp)
            for target in (root / "wiki", output / ".git"):
                with self.assertRaises(ValueError):
                    wiki.export(root, target, "owner/project")

    def test_invalid_owned_paths_cannot_delete_outside_export(self):
        with tempfile.TemporaryDirectory() as temp:
            root, output = self.fixture(temp)
            output.mkdir()
            (output / wiki.MARKER).write_text('["../README.md"]')
            with self.assertRaises(ValueError):
                wiki.export(root, output, "owner/project")
            self.assertTrue((root / "README.md").exists())


    def german_source(self, output, pages):
        import json
        output.mkdir(parents=True, exist_ok=True)
        path = output / wiki.GERMAN_SOURCE
        path.write_text(json.dumps({"schemaVersion": 1, "pages": pages}, ensure_ascii=False), encoding="utf-8")
        return path

    def test_german_pages_links_language_switches_and_english_sources_unchanged(self):
        with tempfile.TemporaryDirectory() as temp:
            root, output = self.fixture(temp)
            original = (root / "README.md").read_bytes()
            self.german_source(output, {
                "Home": {"title": "Start", "content": "# Start\n[Anleitung](docs/Guide.md#topic)\n"},
                "Guide": {"title": "Anleitung", "content": '# Anleitung\n<a id="topic"></a>\n## Thema\n[Konfiguration](../src/config/README.md)\n'},
            })
            wiki.export(root, output, "owner/project")
            home = (output / "DE-Home.md").read_text(encoding="utf-8")
            self.assertIn("/wiki/DE-Guide#topic", home)
            self.assertIn("[English](https://github.com/owner/project/wiki/Home)", home)
            self.assertIn("[Deutsch](https://github.com/owner/project/wiki/DE-Home)", (output / "Home.md").read_text())
            self.assertIn("/wiki/Configuration", (output / "DE-Guide.md").read_text())
            self.assertEqual(original, (root / "README.md").read_bytes())
            self.assertFalse(list(root.rglob("DE-*.md")))

    def test_german_source_survives_repeated_exports(self):
        with tempfile.TemporaryDirectory() as temp:
            root, output = self.fixture(temp)
            source = self.german_source(output, {"Home": {"title": "Start", "content": "# Start\n"}})
            wiki.export(root, output, "owner/project")
            before = source.read_bytes()
            wiki.export(root, output, "owner/project")
            self.assertEqual(before, source.read_bytes())
            self.assertTrue((output / "DE-Home.md").is_file())

    def test_missing_german_source_preserves_generated_german_pages(self):
        with tempfile.TemporaryDirectory() as temp:
            root, output = self.fixture(temp)
            source = self.german_source(output, {"Home": {"title": "Start", "content": "# Start\n"}})
            wiki.export(root, output, "owner/project")
            before = (output / "DE-Home.md").read_bytes()
            source.unlink()
            with self.assertRaisesRegex(ValueError, "missing"):
                wiki.export(root, output, "owner/project")
            self.assertEqual(before, (output / "DE-Home.md").read_bytes())

    def test_changed_english_source_marks_german_page_for_review(self):
        import hashlib
        with tempfile.TemporaryDirectory() as temp:
            root, output = self.fixture(temp)
            digest = hashlib.sha256((root / "README.md").read_text(encoding="utf-8").encode("utf-8")).hexdigest()
            self.german_source(output, {"Home": {"title": "Start", "content": "# Start\n", "sourceSha256": digest}})
            wiki.export(root, output, "owner/project")
            self.assertNotIn("Hinweis:", (output / "DE-Home.md").read_text(encoding="utf-8"))
            with (root / "README.md").open("a", encoding="utf-8") as stream:
                stream.write("\nUpdated source\n")
            wiki.export(root, output, "owner/project")
            self.assertIn("Hinweis:", (output / "DE-Home.md").read_text(encoding="utf-8"))

    def test_partial_translation_sidebar_avoids_missing_german_home(self):
        with tempfile.TemporaryDirectory() as temp:
            root, output = self.fixture(temp)
            self.german_source(output, {"Guide": {"title": "Anleitung", "content": "# Anleitung\n"}})
            wiki.export(root, output, "owner/project")
            sidebar = (output / "_Sidebar.md").read_text(encoding="utf-8")
            self.assertIn("[Deutsch](https://github.com/owner/project/wiki/DE-Guide)", sidebar)
            self.assertNotIn("/wiki/DE-Home", sidebar)

    def test_unknown_german_page_is_rejected_before_modifying_output(self):
        with tempfile.TemporaryDirectory() as temp:
            root, output = self.fixture(temp)
            self.german_source(output, {"Unknown": {"title": "Unbekannt", "content": "# Unbekannt\n"}})
            with self.assertRaises(ValueError):
                wiki.export(root, output, "owner/project")
            self.assertFalse((output / "Home.md").exists())

    def test_german_names_cannot_overwrite_an_english_page(self):
        with tempfile.TemporaryDirectory() as temp:
            root, output = self.fixture(temp)
            (root / "docs/DE-Guide.md").write_text("# Existing English page")
            self.german_source(output, {"Guide": {"title": "Anleitung", "content": "# Anleitung"}})
            with self.assertRaisesRegex(ValueError, "collision"):
                wiki.export(root, output, "owner/project")
            self.assertFalse((output / "Home.md").exists())

if __name__ == "__main__":
    unittest.main()
