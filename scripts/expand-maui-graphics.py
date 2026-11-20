#!/usr/bin/env python3
"""Write FamilyOS MAUI graphics into Resources/ from committed embedded JSON."""
from __future__ import annotations

import base64
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MAUI_RES = ROOT / "src" / "FamilyOS.Maui" / "Resources"
DATA_DIR = ROOT / "scripts" / "maui-graphics"


def write_embedded() -> int:
    files = sorted(DATA_DIR.glob("embedded_*.json"))
    if not files:
        return 0
    count = 0
    for fp in files:
        data = json.loads(fp.read_text())
        for rel, b64 in data.items():
            dest = MAUI_RES / rel
            dest.parent.mkdir(parents=True, exist_ok=True)
            dest.write_bytes(base64.b64decode(b64))
            count += 1
            print(f"  wrote {dest.relative_to(ROOT)} ({dest.stat().st_size} bytes)")
    return count


def stage_fallback() -> bool:
    script = ROOT / "scripts" / "stage-maui-brand-assets.sh"
    if not script.is_file():
        return False
    try:
        subprocess.check_call(["bash", str(script)], cwd=ROOT)
        return True
    except subprocess.CalledProcessError:
        return False


def main() -> int:
    MAUI_RES.mkdir(parents=True, exist_ok=True)
    n = write_embedded()
    if n:
        print(f"Expanded {n} MAUI graphic assets")
        return 0
    print("No embedded_*.json found; trying stage-maui-brand-assets.sh", file=sys.stderr)
    if stage_fallback():
        return 0
    print("::error::MAUI graphics missing", file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
