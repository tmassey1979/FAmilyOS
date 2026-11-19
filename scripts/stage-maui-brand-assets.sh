#!/usr/bin/env bash
# Stage and optimize FamilyOS brand assets for .NET MAUI AppIcon + Splash.
#
# Goals:
#  - Android 12+ splash: logo fits inside the system circular mask (safe ~66% of canvas)
#  - Adaptive icon foreground: logo inset so it is not clipped by the mask
#  - Compressed RGB/RGBA PNGs (no huge 1284px sources shipped as-is)
#  - Deterministic sizes so MauiSplashScreen BaseSize matches the bitmap
#
# Sources (preferred):
#   mobile/assets/brand/icon-app-1024.png
#   mobile/assets/splash-icon.png / icon.png
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APPICON_DIR="$ROOT/src/FamilyOS.Maui/Resources/AppIcon"
SPLASH_DIR="$ROOT/src/FamilyOS.Maui/Resources/Splash"
mkdir -p "$APPICON_DIR" "$SPLASH_DIR"

# Canvas sizes (1x logical). MAUI resizes per density from BaseSize.
ICON_SIZE=1024
SPLASH_SIZE=512
# Logo occupies this fraction of the canvas (rest is padding / safe zone).
ICON_LOGO_RATIO=0.72
SPLASH_LOGO_RATIO=0.55
NAVY="#0F172A"

find_src() {
  local c
  for c in "$@"; do
    if [[ -f "$c" ]]; then
      echo "$c"
      return 0
    fi
  done
  return 1
}

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

python3 - "$ICON_SRC" "$SPLASH_SRC" "$APPICON_DIR" "$SPLASH_DIR" "$ICON_SIZE" "$SPLASH_SIZE" \
  "$ICON_LOGO_RATIO" "$SPLASH_LOGO_RATIO" "$NAVY" <<'PY'
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    import subprocess
    subprocess.check_call([sys.executable, "-m", "pip", "install", "pillow", "-q"])
    from PIL import Image

icon_src, splash_src, appicon_dir, splash_dir = sys.argv[1:5]
icon_size, splash_size = int(sys.argv[5]), int(sys.argv[6])
icon_ratio, splash_ratio = float(sys.argv[7]), float(sys.argv[8])
navy_hex = sys.argv[9].lstrip("#")
navy = tuple(int(navy_hex[i:i+2], 16) for i in (0, 2, 4)) + (255,)


def load(path: str) -> Image.Image | None:
    if not path:
        return None
    p = Path(path)
    if not p.is_file():
        return None
    im = Image.open(p).convert("RGBA")
    return im


def fit_logo(logo: Image.Image, canvas: int, ratio: float, background: tuple[int, int, int, int]) -> Image.Image:
    """Center logo on a square canvas with transparent or solid padding (safe zone)."""
    out = Image.new("RGBA", (canvas, canvas), background)
    target = max(1, int(canvas * ratio))
    # Preserve aspect ratio inside the target box
    logo = logo.copy()
    logo.thumbnail((target, target), Image.Resampling.LANCZOS)
    x = (canvas - logo.width) // 2
    y = (canvas - logo.height) // 2
    out.alpha_composite(logo, (x, y))
    return out


def save_png(im: Image.Image, path: Path, *, flatten_navy: bool = False) -> None:
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


def solid(canvas: int) -> Image.Image:
    return Image.new("RGBA", (canvas, canvas), navy)


logo_icon = load(icon_src)
logo_splash = load(splash_src) or logo_icon

# --- App icon (full-bleed brand plate for legacy + launcher) ---
appicon_path = Path(appicon_dir) / "appicon.png"
fg_path = Path(appicon_dir) / "appicon_foreground.png"

if logo_icon is not None:
    # Full icon: logo on navy (matches FamilyOS #0F172A brand plate)
    full = fit_logo(logo_icon, icon_size, ratio=0.92, background=navy)
    save_png(full, appicon_path, flatten_navy=True)
    # Adaptive foreground: more padding so OEM masks do not crop the mark
    fg = fit_logo(logo_icon, icon_size, ratio=icon_ratio, background=(0, 0, 0, 0))
    save_png(fg, fg_path, flatten_navy=False)
else:
    save_png(solid(icon_size), appicon_path, flatten_navy=True)
    save_png(solid(icon_size), fg_path, flatten_navy=False)
    print("::warning::No brand icon source; navy placeholders used")

# --- Splash: transparent logo on transparent canvas; Color=#0F172A fills background ---
# Android 12 centers this in a circle ~2/3 of the screen width; ratio 0.55 keeps safe margin.
splash_path = Path(splash_dir) / "splash.png"
if logo_splash is not None:
    splash = fit_logo(logo_splash, splash_size, ratio=splash_ratio, background=(0, 0, 0, 0))
    save_png(splash, splash_path, flatten_navy=False)
else:
    # Tiny centered mark so MAUI still has an image; mostly solid navy from Color=
    mark = Image.new("RGBA", (splash_size, splash_size), (0, 0, 0, 0))
    save_png(mark, splash_path, flatten_navy=False)
    print("::warning::No splash source; empty transparent splash (navy Color only)")
PY

echo "Staged MAUI brand assets:"
ls -la "$APPICON_DIR" "$SPLASH_DIR"
