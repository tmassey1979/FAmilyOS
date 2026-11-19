#!/usr/bin/env bash
# Copy official FamilyOS brand assets from mobile/ into the MAUI resource folders.
# Sources (preferred order):
#   1) mobile/assets/brand/icon-app-1024.png  (primary app icon)
#   2) mobile/assets/icon.png / splash-icon.png
#   3) solid navy placeholder PNG if nothing else is available
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APPICON_DIR="$ROOT/src/FamilyOS.Maui/Resources/AppIcon"
SPLASH_DIR="$ROOT/src/FamilyOS.Maui/Resources/Splash"
mkdir -p "$APPICON_DIR" "$SPLASH_DIR"

ICON_SRC=""
for candidate in \
  "$ROOT/mobile/assets/brand/icon-app-1024.png" \
  "$ROOT/mobile/assets/icon.png" \
  "$ROOT/mobile/assets/brand/play-icon-512.png"
do
  if [[ -f "$candidate" ]]; then
    ICON_SRC="$candidate"
    break
  fi
done

SPLASH_SRC=""
for candidate in \
  "$ROOT/mobile/assets/splash-icon.png" \
  "$ROOT/mobile/assets/brand/icon-app-1024.png" \
  "$ROOT/mobile/assets/icon.png"
do
  if [[ -f "$candidate" ]]; then
    SPLASH_SRC="$candidate"
    break
  fi
done

write_placeholder() {
  local dest="$1"
  # Minimal 1x1 navy PNG (#0F172A) so the build never fails without brand files.
  python3 - "$dest" <<'PY'
import base64, pathlib, sys
png = base64.b64decode(
    # 8x8 solid #0F172A PNG
    "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAYAAADED76LAAAALElEQVR42mNgGAWjYBSMAigA"
    "8f8/A0jgP4j8z8DAwADiMwAxAxAzMDAw/AcAHwME/x+u3Y0AAAAASUVORK5CYII="
)
pathlib.Path(sys.argv[1]).write_bytes(png)
print(f"wrote placeholder {sys.argv[1]}")
PY
}

if [[ -n "$ICON_SRC" ]]; then
  cp "$ICON_SRC" "$APPICON_DIR/appicon.png"
  cp "$ICON_SRC" "$APPICON_DIR/appicon_foreground.png"
  echo "AppIcon from $ICON_SRC"
else
  write_placeholder "$APPICON_DIR/appicon.png"
  cp "$APPICON_DIR/appicon.png" "$APPICON_DIR/appicon_foreground.png"
  echo "::warning::No brand icon found; using navy placeholder"
fi

if [[ -n "$SPLASH_SRC" ]]; then
  cp "$SPLASH_SRC" "$SPLASH_DIR/splash.png"
  echo "Splash from $SPLASH_SRC"
else
  write_placeholder "$SPLASH_DIR/splash.png"
  echo "::warning::No splash asset found; using navy placeholder"
fi

ls -la "$APPICON_DIR" "$SPLASH_DIR"
