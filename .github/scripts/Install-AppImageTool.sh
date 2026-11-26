#!/usr/bin/env bash
set -euo pipefail
tool_dir="$RUNNER_TEMP/appimage-tools"
mkdir -p "$tool_dir"
curl --fail --location --retry 3 \
  https://github.com/AppImage/appimagetool/releases/download/1.9.1/appimagetool-x86_64.AppImage \
  --output "$tool_dir/appimagetool"
echo "ed4ce84f0d9caff66f50bcca6ff6f35aae54ce8135408b3fa33abfc3cb384eb0  $tool_dir/appimagetool" | sha256sum --check
chmod +x "$tool_dir/appimagetool"
echo "$tool_dir" >> "$GITHUB_PATH"
