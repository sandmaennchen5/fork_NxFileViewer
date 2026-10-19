# License inventory maintenance

CREDITS.txt lists direct and transitive runtime packages from
src/NxFileViewer/obj/project.assets.json. When updating a package, review its
NuGet metadata and upstream notices, update CREDITS.txt and the files here, then
regenerate docs/THIRD-PARTY-NOTICES.txt. Build/test tools and optional external
plugins are outside this runtime inventory.

Sources:
- WindowsAPICodePack.txt: contre/Windows-API-Code-Pack-1.1, commit
  6822150a32359aa7462f4cf86f04af129db6dcd0, LICENSE.
- SharpCompress.txt: adamhathcock/sharpcompress, tag 0.50.3, LICENSE.txt.
- ZstdSharp.txt: oleg-st/ZstdSharp, tag 0.8.8, LICENSE.
- DotNet.txt: dotnet/runtime, tag v10.0.0, LICENSE.TXT (MIT).
- DotNet-ThirdParty.txt: the original THIRD-PARTY-NOTICES.TXT shipped in
  Microsoft.Extensions.Logging 10.0.11.
- LibHac.txt: standard MIT license text with the copyright notice and MIT
  expression recorded in the LibHac 0.19.0 NuGet manifest. The upstream source
  license at the manifest's recorded commit was unavailable (HTTP 404).

All library package copyright metadata:
LibHac: Copyright © 2018-2022 Alex Barney.
Windows API Code Pack packages: Copyright © 2020.
Microsoft packages: © Microsoft Corporation. All rights reserved.
SharpCompress 0.50.3 package: Copyright (c) 2025 Adam Hathcock.
ZstdSharp.Port 0.8.8 package: Copyright Oleg Stepanischev 2026.
The original upstream license files retain their original copyright years.

Known attribution gap: inherited artwork names Iconfinder but does not identify
individual assets, authors or licenses. No license is inferred for these assets.
This inventory is not a declaration that all asset provenance is resolved.