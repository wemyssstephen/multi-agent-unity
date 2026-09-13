import tomllib
from functools import lru_cache
from pathlib import Path

TASKS_FILE = Path(__file__).parent / "tasks.toml"

@lru_cache
def _load() -> dict:
    with TASKS_FILE.open("rb") as f:
        return tomllib.load(f)

def get_task(genre: str, condition: str) -> str:
    """Prompt text example: ("sidesceoller", "empty")"""
    try:
        raw = _load()[genre][condition]["task"]
    except KeyError:
        raise KeyError(f"no task [{genre}.{condition} in {TASKS_FILE}]")
    return " ".join(raw.split())

def get_tests(genre: str) -> list[str]:
    try:
        return list(_load()["tests"][genre])
    except KeyError:
        raise KeyError(f"no test list for genre {genre} in {TASKS_FILE}")
