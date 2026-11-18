from __future__ import annotations

from pathlib import Path

import pytest

from app.config_tables import Tables, load_tables

CONFIG_DIR = Path(__file__).resolve().parent.parent / "config"


@pytest.fixture(scope="session")
def tables() -> Tables:
    return load_tables(CONFIG_DIR)
