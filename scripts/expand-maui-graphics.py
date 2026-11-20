#!/usr/bin/env python3
"""Expand FamilyOS MAUI graphics into Resources/ (AppIcon, Splash, Menu, Brand).

Source priority:
  1) scripts/maui-graphics/*.b64 parts → maui-graphics.tar.gz
  2) scripts/stage-maui-brand-assets.sh (live brand from mobile/assets)

Safe to run repeatedly; overwrites generated files only.
"""
from __future__ import annotations

import base64
import io
import subprocess
import sys
import tarfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MAUI = ROOT / "src" / "FamilyOS.Maui"
PARTS_DIR = ROOT / "scripts" / "maui-graphics"
RESOURCES = MAUI / "Resources"


def expand_bundle() -> bool:
    parts = sorted(PARTS_DIR.glob("part*.b64"))
    if not parts:
        # single-file fallback
        single = PARTS_DIR / "maui-graphics.tar.gz.b64"
        if single.is_file():
            data = base64.b64decode(single.read_text().encode("ascii"))
        else:
            return False
    else:
        buf = "".join(p.read_text() for p in parts)
        data = base64.b64decode(buf.encode("ascii"))

    with tarfile.open(fileobj=io.BytesIO(data), mode="r:gz") as tar:
        # Python 3.12+ has filter=; older ignores kwargs carefully
        try:
            tar.extractall(RESOURCES, filter=tarfile.data_filter)
        except TypeError:
            tar.extractall(RESOURCES)

    expected = [
        RESOURCES / "AppIcon" / "appicon.png",
        RESOURCES / "Splash" / "splash.png",
        RESOURCES / "Images" / "Menu" / "menu_home.png",
        RESOURCES / "Images" / "Brand" / "logo_mark.png",
    ]
    # Bundle stores AppIcon/, Splash/, Menu/, Brand/ at archive root — map Menu/Brand under Images/
    menu_src = RESOURCES / "Menu"
    brand_src = RESOURCES / "Brand"
    menu_dst = RESOURCES / "Images" / "Menu"
    brand_dst = RESOURCES / "Images" / "Brand"
    if menu_src.is_dir():
        menu_dst.mkdir(parents=True, exist_ok=True)
        for p in menu_src.glob("*.png"):
            target = menu_dst / p.name
            target.write_bytes(p.read_bytes())
    if brand_src.is_dir():
        brand_dst.mkdir(parents=True, exist_ok=True)
        for p in brand_src.glob("*.png"):
            (brand_dst / p.name).write_bytes(p.read_bytes())

    ok = (RESOURCES / "AppIcon" / "appicon.png").is_file() and (RESOURCES / "Splash" / "splash.png").is_file()
    print(f"Expanded MAUI graphics bundle → {RESOURCES} (ok={ok})")
    return ok


def stage_from_mobile() -> bool:
    script = ROOT / "scripts" / "stage-maui-brand-assets.sh"
    if not script.is_file():
        return False
    subprocess.check_call(["bash", str(script)], cwd=ROOT)
    return (RESOURCES / "AppIcon" / "appicon.png").is_file()


def main() -> int:
    PARTS_DIR.mkdir(parents=True, exist_ok=True)
    RESOURCES.mkdir(parents=True, exist_ok=True)
    if expand_bundle():
        return 0
    print("No graphics bundle found; falling back to stage-maui-brand-assets.sh", file=sys.stderr)
    if stage_from_mobile():
        return 0
    print("::error::Could not stage MAUI graphics", file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
