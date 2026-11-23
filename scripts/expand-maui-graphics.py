#!/usr/bin/env python3
"""Produce FamilyOS MAUI graphics under src/FamilyOS.Maui/Resources/.

1) Generates solid navy menu icons (Pillow)
2) Optionally writes embedded_*.json overlays
3) Builds AppIcon, Splash, and Brand logos from mobile/assets
"""
from __future__ import annotations

import base64
import json
import math
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MAUI_RES = ROOT / "src" / "FamilyOS.Maui" / "Resources"
DATA_DIR = ROOT / "scripts" / "maui-graphics"
NAVY = (15, 23, 42, 255)
SIZE = 128


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


def blank():
    from PIL import Image

    return Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))


def generate_menu_icons() -> int:
    """Always write high-contrast navy menu icons so tab/module chrome is visible."""
    ensure_pillow()
    from PIL import Image, ImageDraw

    menu_dir = MAUI_RES / "Images" / "Menu"
    menu_dir.mkdir(parents=True, exist_ok=True)
    written = 0

    def save(name: str, im):
        nonlocal written
        dest = menu_dir / name
        im.save(dest, "PNG", optimize=True)
        written += 1
        print(f"  menu {dest.relative_to(ROOT)} ({dest.stat().st_size}b)")

    # home
    im = blank(); d = ImageDraw.Draw(im)
    d.polygon([(64, 20), (108, 56), (96, 56), (96, 100), (32, 100), (32, 56), (20, 56)], fill=NAVY)
    save("menu_home.png", im)

    # tasks
    im = blank(); d = ImageDraw.Draw(im)
    d.rounded_rectangle([28, 24, 100, 104], radius=10, outline=NAVY, width=6)
    d.line([(44, 48), (56, 60), (76, 40)], fill=NAVY, width=6)
    d.line([(44, 72), (80, 72)], fill=NAVY, width=6)
    d.line([(44, 88), (70, 88)], fill=NAVY, width=6)
    save("menu_tasks.png", im)

    # calendar
    im = blank(); d = ImageDraw.Draw(im)
    d.rounded_rectangle([28, 32, 100, 104], radius=10, outline=NAVY, width=6)
    d.rectangle([28, 32, 100, 52], fill=NAVY)
    d.line([(48, 22), (48, 42)], fill=NAVY, width=6)
    d.line([(80, 22), (80, 42)], fill=NAVY, width=6)
    for x, y in [(44, 68), (64, 68), (84, 68), (44, 86), (64, 86)]:
        d.ellipse([x - 4, y - 4, x + 4, y + 4], fill=NAVY)
    save("menu_calendar.png", im)

    # family
    im = blank(); d = ImageDraw.Draw(im)
    d.ellipse([44, 24, 84, 64], outline=NAVY, width=6)
    d.arc([28, 64, 100, 120], 200, 340, fill=NAVY, width=6)
    save("menu_family.png", im)

    # more
    im = blank(); d = ImageDraw.Draw(im)
    for x in (36, 64, 92):
        d.ellipse([x - 10, 54, x + 10, 74], fill=NAVY)
    save("menu_more.png", im)

    # requests
    im = blank(); d = ImageDraw.Draw(im)
    d.rounded_rectangle([24, 28, 104, 84], radius=14, outline=NAVY, width=6)
    d.polygon([(48, 84), (40, 108), (68, 84)], fill=NAVY)
    save("menu_requests.png", im)

    # meals
    im = blank(); d = ImageDraw.Draw(im)
    d.ellipse([28, 40, 100, 100], outline=NAVY, width=6)
    d.arc([40, 28, 88, 60], 0, 180, fill=NAVY, width=5)
    save("menu_meals.png", im)

    # chat
    im = blank(); d = ImageDraw.Draw(im)
    d.rounded_rectangle([24, 30, 100, 82], radius=12, outline=NAVY, width=6)
    d.rounded_rectangle([40, 58, 104, 100], radius=12, outline=NAVY, width=5)
    save("menu_chat.png", im)

    # notifications
    im = blank(); d = ImageDraw.Draw(im)
    d.arc([36, 28, 92, 88], 0, 180, fill=NAVY, width=6)
    d.line([(36, 70), (92, 70)], fill=NAVY, width=6)
    d.ellipse([56, 88, 72, 104], outline=NAVY, width=5)
    save("menu_notifications.png", im)

    # settings
    im = blank(); d = ImageDraw.Draw(im)
    d.ellipse([40, 40, 88, 88], outline=NAVY, width=8)
    d.ellipse([54, 54, 74, 74], fill=NAVY)
    for ang in range(0, 360, 45):
        a = math.radians(ang)
        x1, y1 = 64 + 34 * math.cos(a), 64 + 34 * math.sin(a)
        x2, y2 = 64 + 48 * math.cos(a), 64 + 48 * math.sin(a)
        d.line([(x1, y1), (x2, y2)], fill=NAVY, width=8)
    save("menu_settings.png", im)

    # outcomes
    im = blank(); d = ImageDraw.Draw(im)
    pts = []
    for i in range(5):
        a = math.radians(-90 + i * 72)
        pts.append((64 + 40 * math.cos(a), 64 + 40 * math.sin(a)))
        a2 = math.radians(-90 + i * 72 + 36)
        pts.append((64 + 18 * math.cos(a2), 64 + 18 * math.sin(a2)))
    d.polygon(pts, fill=NAVY)
    save("menu_outcomes.png", im)

    # gallery
    im = blank(); d = ImageDraw.Draw(im)
    d.rounded_rectangle([24, 32, 104, 100], radius=10, outline=NAVY, width=6)
    d.ellipse([40, 48, 60, 68], outline=NAVY, width=4)
    d.polygon([(48, 92), (72, 60), (100, 92)], fill=NAVY)
    save("menu_gallery.png", im)

    # map pin
    im = blank(); d = ImageDraw.Draw(im)
    d.ellipse([40, 24, 88, 72], outline=NAVY, width=6)
    d.polygon([(40, 56), (64, 108), (88, 56)], fill=NAVY)
    d.ellipse([54, 40, 74, 60], fill=NAVY)
    save("menu_map.png", im)

    # shopping
    im = blank(); d = ImageDraw.Draw(im)
    d.line([(28, 36), (40, 36), (52, 80), (96, 80)], fill=NAVY, width=6)
    d.line([(52, 80), (52, 48), (100, 48), (96, 80)], fill=NAVY, width=6)
    d.ellipse([56, 92, 68, 104], fill=NAVY)
    d.ellipse([84, 92, 96, 104], fill=NAVY)
    save("menu_shopping.png", im)

    return written


def write_embedded() -> int:
    files = sorted(DATA_DIR.glob("embedded_*.json"))
    count = 0
    for fp in files:
        try:
            data = json.loads(fp.read_text())
        except Exception as ex:
            print(f"  skip {fp.name}: {ex}")
            continue
        if not isinstance(data, dict):
            continue
        for rel, b64 in data.items():
            if not isinstance(rel, str) or "/" not in rel or not isinstance(b64, str):
                continue
            try:
                raw = base64.b64decode(b64)
            except Exception:
                continue
            if len(raw) < 50:
                continue
            dest = MAUI_RES / rel
            dest.parent.mkdir(parents=True, exist_ok=True)
            dest.write_bytes(raw)
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
    n_menu = generate_menu_icons()
    n_embed = write_embedded()
    n_mobile = build_from_mobile()
    total = n_menu + n_embed + n_mobile
    appicon = MAUI_RES / "AppIcon" / "appicon.png"
    splash = MAUI_RES / "Splash" / "splash.png"
    if not appicon.is_file() or not splash.is_file():
        print("::error::MAUI AppIcon/Splash missing after expand", file=sys.stderr)
        return 1
    print(f"MAUI graphics ready ({total} files written)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
