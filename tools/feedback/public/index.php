<?php require_once dirname(__DIR__).'/store/web-language.php'; ?>
<!doctype html>
<html lang="<?=webLanguage()?>">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="referrer" content="no-referrer">
<meta http-equiv="Content-Security-Policy" content="default-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self'; object-src 'none'; base-uri 'none'">
<title>NxFileViewer Fork</title>
<style>
:root{color-scheme:dark;font-family:system-ui,-apple-system,"Segoe UI",sans-serif;background:#080e12;color:#eaf5f7}
*{box-sizing:border-box}body{margin:0;min-height:100vh;display:grid;place-items:center;padding:clamp(18px,4vw,64px);background:radial-gradient(ellipse at 50% 30%,#103138 0,transparent 65%),#080e12}
main{width:min(100%,1040px)}img{display:block;width:100%;height:auto;border-radius:20px;border:1px solid #21444c;box-shadow:0 28px 90px #0008}
nav{display:flex;justify-content:center;flex-wrap:wrap;gap:14px;margin-top:28px}a{display:inline-flex;align-items:center;justify-content:center;min-width:156px;min-height:52px;padding:12px 28px;border:1px solid #33515a;border-radius:12px;background:#102129;color:#eaf5f7;text-decoration:none;font-weight:650;transition:background .15s,border-color .15s}
a:hover{background:#17343d;border-color:#03d8dc}a:focus-visible{outline:3px solid #03d8dc;outline-offset:4px}
@media(max-width:480px){nav{gap:10px;margin-top:20px}a{flex:1;min-width:100px;padding:12px 18px}img{border-radius:12px}}
nav[aria-label="Language"] a{min-width:auto;min-height:32px;padding:4px 12px;font-size:.9rem}
</style>
</head>
<body>
<main>
<?php webLanguageSelector(); ?>
<img src="/assets/social-preview.png" alt="NxFileViewer Fork" width="1280" height="640">
<nav aria-label="Repositories">
<a href="https://github.com/sandmaennchen5/fork_NxFileViewer" rel="noopener noreferrer">GitHub</a>
<a href="https://gitlab.com/sandmaennchen5/fork_NxFileViewer" rel="noopener noreferrer">GitLab</a>
<a href="https://git.sandmaennchen5.de/sandmaennchen5/fork_NxFileViewer" rel="noopener noreferrer">Forgejo</a>
</nav>
<nav aria-label="Forums">
<a href="https://gbatemp.net/threads/windows-nxfileviewer-fork-batch-verification-nsz-xcz-support-library-renaming-and-more.685587/" rel="noopener noreferrer">GBAtemp</a>
<a href="https://psxtools.de/forum/index.php?thread/79571-nxfileviewer-ein-tool-zum-auslesen-von-metadaten-nsp-nsz-xci-xcz/" rel="noopener noreferrer">PSXTools</a>
</nav>
</main>
</body>
</html>
