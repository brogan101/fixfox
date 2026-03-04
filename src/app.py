from __future__ import annotations
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
        from .core.brand import APP_NAME, ICON_PNG
        from .core.brand_assets import ensure_logo_on_desktop
        from .core.db import initialize_db
        from .core.logging_setup import configure_logging, install_global_exception_handler
        from .core.settings import load_settings
        from .core.utils import resource_path
        from .ui.app_qss import build_qss
        from .ui.theme import resolve_theme_tokens, set_ui_scale_percent
        from .ui.shell import AppShell
    except ImportError:
        _ensure_repo_on_sys_path()
        from src.core.brand import APP_NAME, ICON_PNG
        from src.core.brand_assets import ensure_logo_on_desktop
        from src.core.db import initialize_db
        from src.core.logging_setup import configure_logging, install_global_exception_handler
        from src.core.settings import load_settings
        from src.core.utils import resource_path
        from src.ui.app_qss import build_qss
        from src.ui.theme import resolve_theme_tokens, set_ui_scale_percent
        from src.ui.shell import AppShell
    return (
        QIcon,
        QApplication,
        APP_NAME,
        ICON_PNG,
        ensure_logo_on_desktop,
        initialize_db,
        configure_logging,
        install_global_exception_handler,
        load_settings,
        resource_path,
        build_qss,
        resolve_theme_tokens,
        set_ui_scale_percent,
        AppShell,
    )


def _load_bundled_font(logger, resource_path) -> None:
    from PySide6.QtGui import QFont, QFontDatabase
    from PySide6.QtWidgets import QApplication

    for rel in ("src/assets/fonts/DejaVuSans.ttf", "assets/fonts/DejaVuSans.ttf"):
        font_id = QFontDatabase.addApplicationFont(str(resource_path(rel)))
        if font_id < 0:
            continue
        families = QFontDatabase.applicationFontFamilies(font_id)
        if not families:
            continue
        app = QApplication.instance()
        if app is not None:
            app.setFont(QFont(families[0]))
        logger.info("Bundled font loaded: %s", families[0])
        return
    logger.warning("Bundled font not found or failed to load.")


def main():
    try:
        (
            QIcon,
            QApplication,
            APP_NAME,
            ICON_PNG,
            ensure_logo_on_desktop,
            initialize_db,
            configure_logging,
            install_global_exception_handler,
            load_settings,
            resource_path,
            build_qss,
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

    app = QApplication(sys.argv)
    app.setApplicationName(APP_NAME)
    app.setWindowIcon(QIcon(resource_path(ICON_PNG)))
    _load_bundled_font(logger, resource_path)
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
    return app.exec()

if __name__ == "__main__":
    raise SystemExit(main())
