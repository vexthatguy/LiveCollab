#!/usr/bin/env python3
"""Assemble the payload layout used by the installers and CI.

Result (repo/build/stage):
  livecollab/
    livecollab_host.py          companion script (all platforms)
    package/                    UPM package (full tree)
      package.json LICENSE README.md CHANGELOG.md Editor/ Tools~/
  livecollab_guide.html         offline doc (Windows installer uses this)
  LiveCollabHost.exe            copied if dist/LiveCollabHost.exe exists

Run from anywhere; paths are relative to the repo root (this file sits in
Tools~/, two levels down from the root). Works on Windows/macOS/Linux.
"""
import pathlib
import shutil
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
STAGE = ROOT / "build" / "stage"
PAYLOAD = STAGE / "livecollab"
PKG = PAYLOAD / "package"


def copy_tree(src: pathlib.Path, dst: pathlib.Path) -> None:
    if src.is_dir():
        shutil.copytree(
            src, dst, dirs_exist_ok=True,
            ignore=shutil.ignore_patterns("__pycache__", "*.pyc", "*.pyo"))
    elif src.is_file():
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst)
    else:
        raise FileNotFoundError(src)


def main() -> int:
    STAGE.mkdir(parents=True, exist_ok=True)
    PAYLOAD.mkdir(parents=True, exist_ok=True)
    PKG.mkdir(parents=True, exist_ok=True)

    # --- UPM package payload ---
    for name in ("package.json", "LICENSE", "README.md", "CHANGELOG.md"):
        copy_tree(ROOT / name, PKG / name)
    copy_tree(ROOT / "Editor", PKG / "Editor")
    copy_tree(ROOT / "Tools~", PKG / "Tools~")

    # --- companion script ---
    copy_tree(ROOT / "Tools~" / "livecollab_host.py", PAYLOAD / "livecollab_host.py")

    # --- optional prebuilt companion binary (used by CI) ---
    exe = ROOT / "dist" / "LiveCollabHost.exe"
    if exe.exists():
        copy_tree(exe, STAGE / "LiveCollabHost.exe")
    uni = ROOT / "dist" / "livecollab_host"
    if uni.exists():
        copy_tree(uni, PAYLOAD / "livecollab_host")

    # --- offline guide (Windows installer) ---
    readme = (ROOT / "README.md").read_text(encoding="utf-8")
    import html
    body = html.escape(readme)
    doc = ("<!DOCTYPE html><html><head><meta charset=\"utf-8\">"
           "<title>LiveCollab — User Guide</title><style>"
           "body{font-family:Segoe UI,Arial,sans-serif;max-width:900px;margin:24px auto;"
           "padding:0 16px;color:#1b1b1b;line-height:1.55}"
           "h1{color:#0e7c9a}h2{color:#0e7c9a;border-bottom:1px solid #ddd;"
           "padding-bottom:4px;margin-top:36px}"
           "pre{background:#f6f8fa;border:1px solid #e1e4e8;border-radius:6px;"
           "padding:14px;overflow-x:auto;white-space:pre-wrap}</style></head><body><pre>"
           + body + "</pre></body></html>")
    (STAGE / "livecollab_guide.html").write_text(doc, encoding="utf-8")

    print("staged:", sorted(str(p.relative_to(STAGE)) for p in STAGE.rglob("*") if p.is_file()))
    return 0


if __name__ == "__main__":
    sys.exit(main())
