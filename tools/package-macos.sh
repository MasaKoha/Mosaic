#!/bin/sh
set -eu

project_directory=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
case "$(uname -m)" in
    arm64) runtime_identifier=osx-arm64 ;;
    x86_64) runtime_identifier=osx-x64 ;;
    *) echo "対応するMac上で実行してください。" >&2; exit 1 ;;
esac

bundle_directory="$project_directory/artifacts/Game Mock Studio.app"
dotnet publish "$project_directory/GameMockStudio.csproj" \
    --configuration Release --runtime "$runtime_identifier" --self-contained true \
    --output "$bundle_directory/Contents/MacOS" --nologo

cat > "$bundle_directory/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleExecutable</key><string>GameMockStudio</string>
  <key>CFBundleIdentifier</key><string>jp.pisukelab.GameMockStudio</string>
  <key>CFBundleName</key><string>Game Mock Studio</string>
  <key>CFBundleDisplayName</key><string>Game Mock Studio</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleVersion</key><string>1</string>
  <key>CFBundleShortVersionString</key><string>0.2.0</string>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
PLIST
printf '起動用アプリ: %s\n' "$bundle_directory"
