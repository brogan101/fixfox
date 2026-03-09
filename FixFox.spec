# -*- mode: python ; coding: utf-8 -*-

from pathlib import Path

from PyInstaller.utils.hooks import collect_submodules


ROOT = Path.cwd()
SRC = ROOT / "src"
ASSETS = SRC / "assets"

hiddenimports = collect_submodules("src")

a = Analysis(
    ["src/app.py"],
    pathex=[str(ROOT)],
    binaries=[],
    datas=[(str(ASSETS), "assets")],
    hiddenimports=hiddenimports,
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
    noarchive=False,
    optimize=0,
)
pyz = PYZ(a.pure)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name="FixFox",
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    console=False,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
    icon=str(ASSETS / "brand" / "fixfox_icon.ico"),
    version=str(ROOT / "packaging" / "fixfox_version_info.txt"),
)

coll = COLLECT(
    exe,
    a.binaries,
    a.datas,
    strip=False,
    upx=True,
    upx_exclude=[],
    name="FixFox",
)
