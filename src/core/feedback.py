from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path

from .paths import ensure_dirs


def save_feedback(name: str, email: str, category: str, message: str) -> Path:
    dirs = ensure_dirs()
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
    target = dirs["feedback"] / f"feedback_{stamp}.json"
    payload = {
        "created_utc": datetime.now(timezone.utc).isoformat(),
        "name": name,
        "email": email,
        "category": category,
        "message": message,
    }
    target.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    return target
