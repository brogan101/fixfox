from __future__ import annotations
import os
from pathlib import Path
import sys

INSTALL_CMD = "py -m pip install -r requirements.txt"
MISSING_DEPS = {"PySide6", "psutil"}


def _ensure_repo_on_sys_path() -> None:
    # Supports direct script execution: `python src/app.py`.
    repo_root = str(Path(__file__).resolve().parent.parent)
    if repo_root not in sys.path:
        sys.path.insert(0, repo_root)


def _show_dependency_error() -> None:
    app_display_name = "Fix Fox"
    try:
        from .core.brand import APP_DISPLAY_NAME
        app_display_name = APP_DISPLAY_NAME
    except Exception:
        try:
            _ensure_repo_on_sys_path()
            from src.core.brand import APP_DISPLAY_NAME
            app_display_name = APP_DISPLAY_NAME
        except Exception:
            ...

    message = (
        "Required dependencies are missing.\n\n"
        "Run this command in the project folder:\n"
        f"{INSTALL_CMD}"
    )
    title = f"{app_display_name} Setup Required"

    if sys.platform == "win32":
        try:
            import ctypes
            ctypes.windll.user32.MessageBoxW(None, message, title, 0x10)
            return
        except Exception as exc:
            print(f"Dependency error dialog failed: {exc}", file=sys.stderr)

    print(f"{title}\n\n{message}", file=sys.stderr)


def _is_missing_required_dependency(exc: BaseException) -> bool:
    if isinstance(exc, ModuleNotFoundError):
        return getattr(exc, "name", None) in MISSING_DEPS
    if isinstance(exc, ImportError):
        text = str(exc)
        return any(dep in text for dep in MISSING_DEPS)
    return False


def _load_runtime_imports():
    from PySide6.QtGui import QIcon
    from PySide6.QtWidgets import QApplication
    try:
        from .core.brand import APP_NAME, ICON_ICO, ICON_PNG
        from .core.brand_assets import ensure_logo_on_desktop
        from .core.db import initialize_db
        from .core.logging_setup import configure_logging, install_global_exception_handler
        from .core.settings import load_settings
        from .core.utils import resource_path
        from .ui.app_qss import build_qss
        from .ui.font_utils import font_asset_candidates
        from .ui.theme import resolve_theme_tokens, set_ui_scale_percent
        from .ui.shell import AppShell
    except ImportError:
        _ensure_repo_on_sys_path()
        from src.core.brand import APP_NAME, ICON_ICO, ICON_PNG
        from src.core.brand_assets import ensure_logo_on_desktop
        from src.core.db import initialize_db
        from src.core.logging_setup import configure_logging, install_global_exception_handler
        from src.core.settings import load_settings
        from src.core.utils import resource_path
        from src.ui.app_qss import build_qss
        from src.ui.font_utils import font_asset_candidates
        from src.ui.theme import resolve_theme_tokens, set_ui_scale_percent
        from src.ui.shell import AppShell
    return (
        QIcon,
        QApplication,
        APP_NAME,
        ICON_ICO,
        ICON_PNG,
        ensure_logo_on_desktop,
        initialize_db,
        configure_logging,
        install_global_exception_handler,
        load_settings,
        resource_path,
        build_qss,
        font_asset_candidates,
        resolve_theme_tokens,
        set_ui_scale_percent,
        AppShell,
    )


def _is_valid_font_blob(blob: bytes) -> bool:
    if len(blob) <= 50 * 1024:
        return False
    magic = blob[:4]
    return magic == b"\x00\x01\x00\x00" or magic == b"OTTO"


def _load_bundled_font(logger, font_asset_candidates) -> str:
    from PySide6.QtCore import QByteArray
    from PySide6.QtGui import QFont, QFontDatabase
    from PySide6.QtWidgets import QApplication

    app = QApplication.instance()
    if app is None:
        return "Segoe UI"

    for path in font_asset_candidates("NotoSans-Regular.ttf"):
        try:
            if not path.exists():
                continue
            raw = path.read_bytes()
            if not _is_valid_font_blob(raw):
                logger.warning("Font validation failed for %s", path)
                continue
            font_id = QFontDatabase.addApplicationFontFromData(QByteArray(raw))
            if font_id < 0:
                logger.warning("Qt rejected font data from %s", path)
                continue
            families = QFontDatabase.applicationFontFamilies(font_id)
            if not families:
                logger.warning("Qt returned no families for %s", path)
                continue
            family = str(families[0]).strip() or "Segoe UI"
            app.setFont(QFont(family))
            logger.info("Selected UI font: %s (%s)", family, path)
            print(f"[FixFox] UI font: {family}")
            return family
        except Exception as exc:
            logger.warning("Font load failed for %s: %s", path, exc)
    fallback = "Segoe UI"
    app.setFont(QFont(fallback))
    logger.info("Selected UI font fallback: %s", fallback)
    print(f"[FixFox] UI font: {fallback}")
    return fallback


def _apply_windows11_corner_hint(widget) -> None:
    if sys.platform != "win32":
        return
    try:
        import ctypes

        DWMWA_WINDOW_CORNER_PREFERENCE = 33
        DWMWCP_ROUND = 2
        value = ctypes.c_int(DWMWCP_ROUND)
        hwnd = int(widget.winId())
        ctypes.windll.dwmapi.DwmSetWindowAttribute(  # type: ignore[attr-defined]
            ctypes.c_void_p(hwnd),
            ctypes.c_uint(DWMWA_WINDOW_CORNER_PREFERENCE),
            ctypes.byref(value),
            ctypes.sizeof(value),
        )
    except Exception:
        return


def main():
    try:
        (
            QIcon,
            QApplication,
            APP_NAME,
            ICON_ICO,
            ICON_PNG,
            ensure_logo_on_desktop,
            initialize_db,
            configure_logging,
            install_global_exception_handler,
            load_settings,
            resource_path,
            build_qss,
            font_asset_candidates,
            resolve_theme_tokens,
            set_ui_scale_percent,
            AppShell,
        ) = _load_runtime_imports()
    except (ImportError, ModuleNotFoundError) as exc:
        if _is_missing_required_dependency(exc):
            _show_dependency_error()
            return 1
        raise

    logger = configure_logging()
    install_global_exception_handler(logger)
    logger.info("Starting %s", APP_NAME)
    try:
        initialize_db()
    except Exception as exc:
        logger.warning("DB initialization warning: %s", exc)

    from PySide6.QtCore import QTimer, Qt

    QApplication.setHighDpiScaleFactorRoundingPolicy(Qt.HighDpiScaleFactorRoundingPolicy.PassThrough)
    app = QApplication(sys.argv)
    app.setApplicationName(APP_NAME)
    icon_path = resource_path(ICON_ICO)
    app_icon = QIcon(icon_path)
    if app_icon.isNull():
        app_icon = QIcon(resource_path(ICON_PNG))
    app.setWindowIcon(app_icon)
    _load_bundled_font(logger, font_asset_candidates)
    try:
        ensure_logo_on_desktop(overwrite=False)
    except Exception as exc:
        logger.warning("Desktop logo setup skipped: %s", exc)
    settings = load_settings()
    set_ui_scale_percent(getattr(settings, "ui_scale_pct", 100))
    tokens = resolve_theme_tokens(settings.theme_palette, settings.theme_mode)
    app.setStyleSheet(build_qss(tokens, settings.theme_mode, settings.density))
    w = AppShell()
    w.show()
    QTimer.singleShot(0, lambda: _apply_windows11_corner_hint(w))
    try:
        auto_exit_ms = int(os.environ.get("FIXFOX_AUTO_EXIT_MS", "0").strip() or "0")
    except Exception:
        auto_exit_ms = 0
    if auto_exit_ms > 0:
        QTimer.singleShot(max(200, auto_exit_ms), app.quit)
    return app.exec()

if __name__ == "__main__":
    raise SystemExit(main())
