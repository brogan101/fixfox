from __future__ import annotations

from pathlib import Path


def main() -> int:
    from src.core.registry import generate_capability_catalog
    from src.core.runbooks import generate_runbook_catalog

    docs = Path("docs")
    docs.mkdir(parents=True, exist_ok=True)
    generate_capability_catalog(docs / "CAPABILITY_CATALOG.md")
    generate_runbook_catalog(str(docs / "RUNBOOK_CATALOG.md"))
    print("Generated docs/CAPABILITY_CATALOG.md and docs/RUNBOOK_CATALOG.md")
    return 0


if __name__ == "__main__":
    if __package__ in {None, ""}:
        print("Run this tool via: python -m scripts.generate_catalogs")
        raise SystemExit(1)
    raise SystemExit(main())
