#!/usr/bin/env python3
"""Produce FamilyOS MAUI graphics under src/FamilyOS.Maui/Resources/.

1) Writes embedded tab icons from scripts/maui-graphics/embedded_*.json
2) Builds AppIcon, Splash, and Brand logos from mobile/assets (Pillow)
"""
from __future__ import annotations

import base64
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MAUI_RES = ROOT / "src" / "FamilyOS.Maui" / "Resources"
DATA_DIR = ROOT / "scripts" / "maui-graphics"
NAVY = (15, 23, 42, 255)


def ensure_pillow():
    try:
        from PIL import Image  # noqa: F401
        return
    except ImportError:
        pass
    import subprocess

    subprocess.check_call([sys.executable, "-m", "pip", "install", "pillow", "-q"])
    try:
        from PIL import Image  # noqa: F401
    except ImportError:
        subprocess.check_call(
            [sys.executable, "-m", "pip", "install", "--break-system-packages", "pillow", "-q"]
        )


def write_embedded() -> int:
    files = sorted(DATA_DIR.glob("embedded_*.json"))
    count = 0
    for fp in files:
        data = json.loads(fp.read_text())
        for rel, b64 in data.items():
            dest = MAUI_RES / rel
            dest.parent.mkdir(parents=True, exist_ok=True)
            dest.write_bytes(base64.b64decode(b64))
            count += 1
            print(f"  embedded {dest.relative_to(ROOT)} ({dest.stat().st_size}b)")
    return count


def find_src(*candidates: str) -> Path | None:
    for c in candidates:
        p = ROOT / c
        if p.is_file() and p.stat().st_size > 100:
            return p
    return None


def fit_logo(logo, size: int, ratio: float, background):
    from PIL import Image

    out = Image.new("RGBA", (size, size), background)
    logo = logo.copy()
    logo.thumbnail((max(1, int(size * ratio)), max(1, int(size * ratio))), Image.Resampling.LANCZOS)
    out.alpha_composite(logo, ((size - logo.width) // 2, (size - logo.height) // 2))
    return out


def build_from_mobile() -> int:
    ensure_pillow()
    from PIL import Image

    written = 0
    icon_path = find_src(
        "mobile/assets/brand/icon-app-1024.png",
        "mobile/assets/icon.png",
        "mobile/assets/adaptive-icon.png",
        "mobile/assets/brand/play-icon-512.png",
    )
    splash_path = find_src(
        "mobile/assets/splash-icon.png",
        "mobile/assets/brand/icon-app-1024.png",
        "mobile/assets/icon.png",
    )
    mark_path = find_src("mobile/assets/brand/icon-mark-48.png")
    word_navy = find_src("mobile/assets/brand/logo-wordmark-navy.png")
    word_black = find_src("mobile/assets/brand/logo-wordmark-black.png")
    lockup = find_src("mobile/assets/brand/logo-lockup-light.png")

    icon = Image.open(icon_path).convert("RGBA") if icon_path else None
    splash_src = Image.open(splash_path).convert("RGBA") if splash_path else icon

    appicon_dir = MAUI_RES / "AppIcon"
    splash_dir = MAUI_RES / "Splash"
    brand_dir = MAUI_RES / "Images" / "Brand"
    appicon_dir.mkdir(parents=True, exist_ok=True)
    splash_dir.mkdir(parents=True, exist_ok=True)
    brand_dir.mkdir(parents=True, exist_ok=True)

    if icon is not None:
        full = fit_logo(icon, 1024, 0.92, NAVY)
        bg = Image.new("RGB", full.size, NAVY[:3])
        bg.paste(full, mask=full.split()[-1])
        bg.save(appicon_dir / "appicon.png", optimize=True)
        fit_logo(icon, 1024, 0.72, (0, 0, 0, 0)).save(appicon_dir / "appicon_foreground.png", optimize=True)
        written += 2
        print("  AppIcon from", icon_path)
    else:
        Image.new("RGB", (512, 512), NAVY[:3]).save(appicon_dir / "appicon.png")
        Image.new("RGBA", (512, 512), NAVY).save(appicon_dir / "appicon_foreground.png")
        written += 2
        print("  AppIcon navy placeholder")

    if splash_src is not None:
        fit_logo(splash_src, 512, 0.55, (0, 0, 0, 0)).save(splash_dir / "splash.png", optimize=True)
    else:
        Image.new("RGBA", (512, 512), (0, 0, 0, 0)).save(splash_dir / "splash.png")
    written += 1
    print("  Splash ready")

    # Brand marks for headers / app bars
    mark = Image.open(mark_path).convert("RGBA") if mark_path else (icon.copy() if icon else None)
    if mark is not None:
        m = mark.copy()
        m.thumbnail((96, 96), Image.Resampling.LANCZOS)
        canvas = Image.new("RGBA", (96, 96), (0, 0, 0, 0))
        canvas.alpha_composite(m, ((96 - m.width) // 2, (96 - m.height) // 2))
        canvas.save(brand_dir / "logo_mark.png", optimize=True)
        written += 1

    for dest_name, src in (
        ("logo_wordmark.png", word_navy or word_black),
        ("logo_wordmark_on_dark.png", lockup or word_black or word_navy),
        ("logo_lockup.png", lockup or word_navy),
    ):
        if not src:
            continue
        im = Image.open(src).convert("RGBA")
        im.thumbnail((480, 120), Image.Resampling.LANCZOS)
        im.save(brand_dir / dest_name, optimize=True)
        written += 1
        print(f"  Brand {dest_name} from {src}")

    return written


def main() -> int:
    MAUI_RES.mkdir(parents=True, exist_ok=True)
    n_embed = write_embedded()
    n_mobile = build_from_mobile()
    total = n_embed + n_mobile
    appicon = MAUI_RES / "AppIcon" / "appicon.png"
    splash = MAUI_RES / "Splash" / "splash.png"
    if not appicon.is_file() or not splash.is_file():
        print("::error::MAUI AppIcon/Splash missing after expand", file=sys.stderr)
        return 1
    print(f"MAUI graphics ready ({total} files written)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
