#!/usr/bin/env bash
# Stage and optimize FamilyOS brand assets for .NET MAUI AppIcon + Splash + menu icons.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APPICON_DIR="$ROOT/src/FamilyOS.Maui/Resources/AppIcon"
SPLASH_DIR="$ROOT/src/FamilyOS.Maui/Resources/Splash"
mkdir -p "$APPICON_DIR" "$SPLASH_DIR"

ensure_pillow() {
  if python3 -c "from PIL import Image" 2>/dev/null; then
    return 0
  fi
  echo "Installing Pillow…"
  python3 -m pip install --user --upgrade pip -q || true
  if python3 -m pip install --user pillow -q 2>/dev/null; then
    return 0
  fi
  if python3 -m pip install --break-system-packages pillow -q 2>/dev/null; then
    return 0
  fi
  # Last resort on Debian/Ubuntu runners
  if command -v apt-get >/dev/null 2>&1; then
    sudo apt-get update -qq && sudo apt-get install -y -qq python3-pil && return 0
  fi
  echo "::error::Could not install Pillow (python3-pil)"
  return 1
}

ensure_pillow

ICON_SIZE=1024
SPLASH_SIZE=512
ICON_LOGO_RATIO=0.72
SPLASH_LOGO_RATIO=0.55
NAVY="#0F172A"

find_src() {
  local c
  for c in "$@"; do
    if [[ -f "$c" ]]; then
      printf '%s' "$c"
      return 0
    fi
  done
  return 1
}

ICON_SRC=""
SPLASH_SRC=""
ICON_SRC="$(find_src \
  "$ROOT/mobile/assets/brand/icon-app-1024.png" \
  "$ROOT/mobile/assets/icon.png" \
  "$ROOT/mobile/assets/adaptive-icon.png" \
  "$ROOT/mobile/assets/brand/play-icon-512.png" || true)"
SPLASH_SRC="$(find_src \
  "$ROOT/mobile/assets/splash-icon.png" \
  "$ROOT/mobile/assets/brand/icon-app-1024.png" \
  "$ROOT/mobile/assets/icon.png" \
  "$ROOT/mobile/assets/adaptive-icon.png" || true)"

echo "ICON_SRC=${ICON_SRC:-<none>}"
echo "SPLASH_SRC=${SPLASH_SRC:-<none>}"

python3 - "${ICON_SRC:-}" "${SPLASH_SRC:-}" "$APPICON_DIR" "$SPLASH_DIR" "$ICON_SIZE" "$SPLASH_SIZE" \
  "$ICON_LOGO_RATIO" "$SPLASH_LOGO_RATIO" "$NAVY" <<'PY'
import sys
from pathlib import Path

from PIL import Image

icon_src, splash_src, appicon_dir, splash_dir = sys.argv[1:5]
icon_size, splash_size = int(sys.argv[5]), int(sys.argv[6])
icon_ratio, splash_ratio = float(sys.argv[7]), float(sys.argv[8])
navy_hex = sys.argv[9].lstrip("#")
navy = tuple(int(navy_hex[i:i+2], 16) for i in (0, 2, 4)) + (255,)

def load(path: str):
    if not path:
        return None
    p = Path(path)
    if not p.is_file():
        return None
    return Image.open(p).convert("RGBA")

def fit_logo(logo, canvas, ratio, background):
    out = Image.new("RGBA", (canvas, canvas), background)
    target = max(1, int(canvas * ratio))
    logo = logo.copy()
    logo.thumbnail((target, target), Image.Resampling.LANCZOS)
    x = (canvas - logo.width) // 2
    y = (canvas - logo.height) // 2
    out.alpha_composite(logo, (x, y))
    return out

def save_png(im, path, *, flatten_navy=False):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    if flatten_navy:
        bg = Image.new("RGB", im.size, navy[:3])
        if im.mode == "RGBA":
            bg.paste(im, mask=im.split()[-1])
        else:
            bg.paste(im)
        bg.save(path, format="PNG", optimize=True)
    else:
        im.save(path, format="PNG", optimize=True)
    print(f"wrote {path} ({path.stat().st_size} bytes, {im.size[0]}x{im.size[1]})")

def solid(canvas):
    return Image.new("RGBA", (canvas, canvas), navy)

logo_icon = load(icon_src)
logo_splash = load(splash_src) or logo_icon

appicon_path = Path(appicon_dir) / "appicon.png"
fg_path = Path(appicon_dir) / "appicon_foreground.png"

if logo_icon is not None:
    full = fit_logo(logo_icon, icon_size, ratio=0.92, background=navy)
    save_png(full, appicon_path, flatten_navy=True)
    fg = fit_logo(logo_icon, icon_size, ratio=icon_ratio, background=(0, 0, 0, 0))
    save_png(fg, fg_path, flatten_navy=False)
else:
    save_png(solid(icon_size), appicon_path, flatten_navy=True)
    save_png(solid(icon_size), fg_path, flatten_navy=False)
    print("::warning::No brand icon source; navy placeholders used")

splash_path = Path(splash_dir) / "splash.png"
if logo_splash is not None:
    splash = fit_logo(logo_splash, splash_size, ratio=splash_ratio, background=(0, 0, 0, 0))
    save_png(splash, splash_path, flatten_navy=False)
else:
    mark = Image.new("RGBA", (splash_size, splash_size), (0, 0, 0, 0))
    save_png(mark, splash_path, flatten_navy=False)
    print("::warning::No splash source; empty transparent splash (navy Color only)")
PY

# Tab + More-menu line icons (monochrome, transparent)
python3 "$ROOT/scripts/generate-maui-menu-icons.py"

echo "Staged MAUI brand + menu assets:"
ls -la "$APPICON_DIR" "$SPLASH_DIR" "$ROOT/src/FamilyOS.Maui/Resources/Images/Menu" 2>/dev/null || true
