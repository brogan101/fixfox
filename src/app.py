from __future__ import annotations
import os
from pathlib import Path
import signal
import sys
import time

INSTALL_CMD = "py -m pip install -r requirements.txt"
MISSING_DEPS = {"PySide6", "psutil"}


def _ensure_repo_on_sys_path() -> None:
    # Supports direct script execution: `python src/app.py`.
    repo_root = str(Path(__file__).resolve().parent.parent)
    if repo_root not in sys.path:
        sys.path.insert(0, repo_root)


def _show_dependency_error() -> None:
    app_display_name = "FixFox"
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
        from .core.perf import PERF_RECORDER
        from .core.qt_runtime import ensure_qt_runtime_env
        from .core.startup_watchdog import install_startup_watchdog
        from .core.ui_freeze_detector import UIFreezeDetector
        from .core.settings import load_settings
        from .core.utils import resource_path
        from .ui.runtime_bootstrap import apply_runtime_ui_bootstrap
        from .ui.shell import AppShell
        from .ui.splash import FixFoxSplashScreen
    except ImportError:
        _ensure_repo_on_sys_path()
        from src.core.brand import APP_NAME, ICON_ICO, ICON_PNG
        from src.core.brand_assets import ensure_logo_on_desktop
        from src.core.db import initialize_db
        from src.core.logging_setup import configure_logging, install_global_exception_handler
        from src.core.perf import PERF_RECORDER
        from src.core.qt_runtime import ensure_qt_runtime_env
        from src.core.startup_watchdog import install_startup_watchdog
        from src.core.ui_freeze_detector import UIFreezeDetector
        from src.core.settings import load_settings
        from src.core.utils import resource_path
        from src.ui.runtime_bootstrap import apply_runtime_ui_bootstrap
        from src.ui.shell import AppShell
        from src.ui.splash import FixFoxSplashScreen
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
        PERF_RECORDER,
        ensure_qt_runtime_env,
        install_startup_watchdog,
        UIFreezeDetector,
        load_settings,
        resource_path,
        apply_runtime_ui_bootstrap,
        AppShell,
        FixFoxSplashScreen,
    )


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
            PERF_RECORDER,
            ensure_qt_runtime_env,
            install_startup_watchdog,
            UIFreezeDetector,
            load_settings,
            resource_path,
            apply_runtime_ui_bootstrap,
            AppShell,
            FixFoxSplashScreen,
        ) = _load_runtime_imports()
    except (ImportError, ModuleNotFoundError) as exc:
        if _is_missing_required_dependency(exc):
            _show_dependency_error()
            return 1
        raise

    logger = configure_logging()
    install_global_exception_handler(logger)
    PERF_RECORDER.reset()
    PERF_RECORDER.set_meta("entrypoint", "src.app.main")
    launch_started = time.perf_counter()
    startup_watchdog = install_startup_watchdog()
    startup_watchdog.mark("logging_ready")
    ensure_qt_runtime_env(logger)
    PERF_RECORDER.set_meta("watchdog_log", str(startup_watchdog.watchdog_log_path))
    PERF_RECORDER.set_meta("qt_log", str(startup_watchdog.qt_log_path))
    logger.info("Starting %s", APP_NAME)

    from PySide6.QtCore import QTimer, Qt

    startup_watchdog.mark("create_qapplication")
    QApplication.setHighDpiScaleFactorRoundingPolicy(Qt.HighDpiScaleFactorRoundingPolicy.PassThrough)
    app = QApplication(sys.argv)
    startup_watchdog.start()
    app.setApplicationName(APP_NAME)
    startup_watchdog.mark("load_icons")
    icon_path = resource_path(ICON_ICO)
    app_icon = QIcon(icon_path)
    if app_icon.isNull():
        app_icon = QIcon(resource_path(ICON_PNG))
    app.setWindowIcon(app_icon)
    startup_watchdog.mark("show_splash")
    splash = FixFoxSplashScreen(status_text="Loading workspace...")
    splash.setWindowIcon(app_icon)
    splash.show()
    app.processEvents()
    PERF_RECORDER.record("startup.splash_visible_ms", (time.perf_counter() - launch_started) * 1000.0)
    try:
        startup_watchdog.mark("initialize_db")
        splash.update_status("Preparing local database...")
        initialize_db()
    except Exception as exc:
        logger.warning("DB initialization warning: %s", exc)
    startup_watchdog.mark("load_settings")
    settings = load_settings()
    startup_watchdog.mark("apply_runtime_ui")
    splash.update_status("Loading fonts and visual system...")
    apply_runtime_ui_bootstrap(app, logger=logger, settings=settings)
    try:
        startup_watchdog.mark("ensure_desktop_logo")
        ensure_logo_on_desktop(overwrite=False)
    except Exception as exc:
        logger.warning("Desktop logo setup skipped: %s", exc)
    startup_watchdog.mark("build_main_window")
    splash.update_status("Building FixFox shell...")
    interactive_recorded = {"done": False}

    def _record_first_interactive() -> None:
        if interactive_recorded["done"]:
            return
        interactive_recorded["done"] = True
        PERF_RECORDER.record("startup.first_interactive_ms", (time.perf_counter() - launch_started) * 1000.0)

    def _startup_phase_router(phase: str) -> None:
        startup_watchdog.set_phase(phase)
        if phase == "mainwindow:warmup_complete":
            _record_first_interactive()

    w = AppShell(startup_phase_cb=_startup_phase_router)
    w._startup_started_perf = launch_started
    startup_watchdog.attach_window(w)
    startup_watchdog.mark("show_window")
    w.show()
    QTimer.singleShot(0, _record_first_interactive)
    splash.finish(w)
    freeze_detector = UIFreezeDetector(watchdog_log_path=startup_watchdog.watchdog_log_path)
    QTimer.singleShot(0, freeze_detector.start)
    QTimer.singleShot(0, lambda: _apply_windows11_corner_hint(w))
    if hasattr(signal, "SIGINT"):
        signal.signal(signal.SIGINT, lambda *_args: app.quit())
    try:
        auto_exit_ms = int(os.environ.get("FIXFOX_AUTO_EXIT_MS", "0").strip() or "0")
    except Exception:
        auto_exit_ms = 0
    if auto_exit_ms > 0:
        QTimer.singleShot(max(200, auto_exit_ms), app.quit)
    try:
        startup_watchdog.mark("enter_event_loop")
        result = app.exec()
        startup_watchdog.mark("event_loop_exit")
        return result
    except KeyboardInterrupt:
        logger.info("KeyboardInterrupt received, exiting cleanly.")
        app.quit()
        return 130
    finally:
        try:
            if freeze_detector is not None:
                freeze_detector.stop()
                PERF_RECORDER.set_meta("ui_freeze_count", freeze_detector.freeze_count)
        except Exception:
            pass
        if startup_watchdog.first_paint_ms > 0:
            PERF_RECORDER.record("startup.ttfp_ms", startup_watchdog.first_paint_ms)
            PERF_RECORDER.record("startup.main_shell_visible_ms", startup_watchdog.first_paint_ms)
        if startup_watchdog.qt_fatal_lines:
            PERF_RECORDER.set_meta("qt_fatal_warnings", startup_watchdog.qt_fatal_lines[:8])
        report = PERF_RECORDER.write_report()
        logger.info("perf_report=%s", report)
        startup_watchdog.stop()

if __name__ == "__main__":
    raise SystemExit(main())
