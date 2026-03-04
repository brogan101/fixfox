from __future__ import annotations
import sys

INSTALL_CMD = "py -m pip install -r requirements.txt"
MISSING_DEPS = {"PySide6", "psutil"}


def _show_dependency_error() -> None:
    from .core.brand import APP_DISPLAY_NAME

    message = (
        "Required dependencies are missing.\n\n"
        "Run this command in the project folder:\n"
        f"{INSTALL_CMD}"
    )
    title = f"{APP_DISPLAY_NAME} Setup Required"

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
        from .core.logging_setup import configure_logging, install_global_exception_handler
        from .core.settings import load_settings
        from .core.utils import resource_path
        from .ui.app_qss import build_qss
        from .ui.theme import resolve_theme_tokens
        from .ui.main_window import MainWindow
    except ImportError:
        from src.core.brand import APP_NAME, ICON_PNG
        from src.core.brand_assets import ensure_logo_on_desktop
        from src.core.logging_setup import configure_logging, install_global_exception_handler
        from src.core.settings import load_settings
        from src.core.utils import resource_path
        from src.ui.app_qss import build_qss
        from src.ui.theme import resolve_theme_tokens
        from src.ui.main_window import MainWindow
    return (
        QIcon,
        QApplication,
        APP_NAME,
        ICON_PNG,
        ensure_logo_on_desktop,
        configure_logging,
        install_global_exception_handler,
        load_settings,
        resource_path,
        build_qss,
        resolve_theme_tokens,
        MainWindow,
    )


def main():
    try:
        (
            QIcon,
            QApplication,
            APP_NAME,
            ICON_PNG,
            ensure_logo_on_desktop,
            configure_logging,
            install_global_exception_handler,
            load_settings,
            resource_path,
            build_qss,
            resolve_theme_tokens,
            MainWindow,
        ) = _load_runtime_imports()
    except (ImportError, ModuleNotFoundError) as exc:
        if _is_missing_required_dependency(exc):
            _show_dependency_error()
            return 1
        raise

    logger = configure_logging()
    install_global_exception_handler(logger)
    logger.info("Starting %s", APP_NAME)

    app = QApplication(sys.argv)
    app.setApplicationName(APP_NAME)
    app.setWindowIcon(QIcon(resource_path(ICON_PNG)))
    try:
        ensure_logo_on_desktop(overwrite=False)
    except Exception as exc:
        logger.warning("Desktop logo setup skipped: %s", exc)
    settings = load_settings()
    tokens = resolve_theme_tokens(settings.theme_palette, settings.theme_mode)
    app.setStyleSheet(build_qss(tokens, settings.theme_mode, settings.density))
    w = MainWindow()
    w.show()
    return app.exec()

if __name__ == "__main__":
    raise SystemExit(main())
