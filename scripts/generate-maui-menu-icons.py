#!/usr/bin/env python3
"""Generate crisp monochrome menu icons for FamilyOS MAUI Shell tabs + More menu.

Outputs 64x64 RGBA PNGs as Resources/Images/menu_{name}.png
"""
from __future__ import annotations

import math
import subprocess
import sys
from pathlib import Path

try:
    from PIL import Image, ImageDraw
except ImportError:
    subprocess.check_call(
        [sys.executable, "-m", "pip", "install", "pillow", "-q"],
    )
    try:
        from PIL import Image, ImageDraw
    except ImportError:
        subprocess.check_call(
            [sys.executable, "-m", "pip", "install", "--break-system-packages", "pillow", "-q"],
        )
        from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "src" / "FamilyOS.Maui" / "Resources" / "Images"
SIZE = 64
STROKE = (248, 250, 252, 255)
WIDTH = 4


def canvas() -> tuple[Image.Image, ImageDraw.ImageDraw]:
    im = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    return im, ImageDraw.Draw(im)


def save(name: str, im: Image.Image) -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    path = OUT / f"menu_{name}.png"
    im.save(path, format="PNG", optimize=True)
    print(f"  {path.relative_to(ROOT)} ({path.stat().st_size}b)")


def icon_home() -> None:
    im, d = canvas()
    d.line([(12, 30), (32, 12)], fill=STROKE, width=WIDTH)
    d.line([(32, 12), (52, 30)], fill=STROKE, width=WIDTH)
    d.line([(18, 28), (18, 52)], fill=STROKE, width=WIDTH)
    d.line([(46, 28), (46, 52)], fill=STROKE, width=WIDTH)
    d.line([(18, 52), (46, 52)], fill=STROKE, width=WIDTH)
    d.rectangle([28, 38, 36, 52], outline=STROKE, width=max(1, WIDTH - 1))
    save("home", im)


def icon_tasks() -> None:
    im, d = canvas()
    d.rounded_rectangle([14, 12, 50, 52], radius=6, outline=STROKE, width=WIDTH)
    for y in (24, 34, 44):
        d.line([(22, y), (30, y)], fill=STROKE, width=max(1, WIDTH - 1))
        d.line([(34, y), (44, y)], fill=STROKE, width=max(1, WIDTH - 1))
    save("tasks", im)


def icon_calendar() -> None:
    im, d = canvas()
    d.rounded_rectangle([12, 16, 52, 52], radius=6, outline=STROKE, width=WIDTH)
    d.line([(12, 28), (52, 28)], fill=STROKE, width=max(1, WIDTH - 1))
    d.line([(22, 12), (22, 22)], fill=STROKE, width=WIDTH)
    d.line([(42, 12), (42, 22)], fill=STROKE, width=WIDTH)
    for x, y in [(22, 36), (32, 36), (42, 36), (22, 44), (32, 44)]:
        d.ellipse([x - 2, y - 2, x + 2, y + 2], fill=STROKE)
    save("calendar", im)


def icon_family() -> None:
    im, d = canvas()
    d.ellipse([16, 14, 28, 26], outline=STROKE, width=max(1, WIDTH - 1))
    d.arc([12, 28, 32, 52], start=0, end=180, fill=STROKE, width=max(1, WIDTH - 1))
    d.ellipse([36, 14, 48, 26], outline=STROKE, width=max(1, WIDTH - 1))
    d.arc([32, 28, 52, 52], start=0, end=180, fill=STROKE, width=max(1, WIDTH - 1))
    save("family", im)



def icon_map() -> None:
    im, d = canvas()
    # Folded map with a centered location marker, matching the other tab icons.
    d.line([(10, 20), (25, 14), (39, 20), (54, 14)], fill=STROKE, width=WIDTH)
    d.line([(10, 20), (10, 50), (25, 44), (39, 50), (54, 44), (54, 14)], fill=STROKE, width=WIDTH)
    d.line([(25, 14), (25, 44)], fill=STROKE, width=max(1, WIDTH - 1))
    d.line([(39, 20), (39, 50)], fill=STROKE, width=max(1, WIDTH - 1))
    d.ellipse([26, 22, 38, 34], outline=STROKE, width=max(1, WIDTH - 1))
    d.polygon([(27, 32), (37, 32), (32, 43)], fill=STROKE)
    save("map", im)


def icon_more() -> None:
    im, d = canvas()
    for x in (20, 32, 44):
        d.ellipse([x - 4, 28, x + 4, 36], fill=STROKE)
    save("more", im)


def icon_requests() -> None:
    im, d = canvas()
    d.rounded_rectangle([16, 12, 48, 52], radius=6, outline=STROKE, width=WIDTH)
    d.line([(24, 24), (40, 24)], fill=STROKE, width=max(1, WIDTH - 1))
    d.line([(24, 34), (40, 34)], fill=STROKE, width=max(1, WIDTH - 1))
    d.line([(24, 44), (34, 44)], fill=STROKE, width=max(1, WIDTH - 1))
    save("requests", im)


def icon_meals() -> None:
    im, d = canvas()
    d.arc([14, 28, 50, 54], start=0, end=180, fill=STROKE, width=WIDTH)
    d.line([(14, 40), (50, 40)], fill=STROKE, width=max(1, WIDTH - 1))
    for x in (26, 32, 38):
        d.arc([x - 4, 14, x + 4, 28], start=200, end=340, fill=STROKE, width=max(1, WIDTH - 1))
    save("meals", im)


def icon_chat() -> None:
    im, d = canvas()
    d.rounded_rectangle([12, 14, 52, 42], radius=8, outline=STROKE, width=WIDTH)
    d.polygon([(24, 42), (20, 52), (34, 42)], outline=STROKE)
    d.line([(24, 42), (34, 42)], fill=STROKE, width=max(1, WIDTH - 1))
    save("chat", im)


def icon_outcomes() -> None:
    im, d = canvas()
    d.rectangle([16, 36, 24, 50], outline=STROKE, width=max(1, WIDTH - 1))
    d.rectangle([28, 24, 36, 50], outline=STROKE, width=max(1, WIDTH - 1))
    d.rectangle([40, 16, 48, 50], outline=STROKE, width=max(1, WIDTH - 1))
    save("outcomes", im)


def icon_notifications() -> None:
    im, d = canvas()
    d.arc([18, 16, 46, 44], start=0, end=180, fill=STROKE, width=WIDTH)
    d.line([(18, 36), (46, 36)], fill=STROKE, width=WIDTH)
    d.line([(32, 14), (32, 18)], fill=STROKE, width=WIDTH)
    d.arc([28, 44, 36, 52], start=0, end=180, fill=STROKE, width=max(1, WIDTH - 1))
    save("notifications", im)


def icon_settings() -> None:
    im, d = canvas()
    cx, cy, r = 32, 32, 10
    d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=STROKE, width=WIDTH)
    for i in range(8):
        ang = math.radians(i * 45)
        x0 = cx + int(math.cos(ang) * 16)
        y0 = cy + int(math.sin(ang) * 16)
        x1 = cx + int(math.cos(ang) * 22)
        y1 = cy + int(math.sin(ang) * 22)
        d.line([(x0, y0), (x1, y1)], fill=STROKE, width=max(1, WIDTH - 1))
    save("settings", im)


def icon_gallery() -> None:
    im, d = canvas()
    d.rounded_rectangle([12, 16, 52, 48], radius=6, outline=STROKE, width=WIDTH)
    d.ellipse([18, 22, 28, 32], outline=STROKE, width=max(1, WIDTH - 1))
    d.polygon([(22, 44), (32, 30), (40, 40), (46, 34), (50, 44)], outline=STROKE)
    save("gallery", im)


def main() -> None:
    print("Generating MAUI menu icons…")
    for fn in (
        icon_home,
        icon_tasks,
        icon_calendar,
        icon_family,
        icon_map,
        icon_more,
        icon_requests,
        icon_meals,
        icon_chat,
        icon_outcomes,
        icon_notifications,
        icon_settings,
        icon_gallery,
    ):
        fn()
    print("Done.")


if __name__ == "__main__":
    main()
