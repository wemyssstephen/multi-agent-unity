import logging
import os

from multi_agent_unity.analyse import write_report
from multi_agent_unity.bench import Bench
from multi_agent_unity.config import BenchPaths

MODELS = {
    "1": ("Claude Opus 5.5", "claude-opus-5-5"),
    "2": ("Claude Sonnet 5", "claude-sonnet-5"),
    "3": ("Claude Haiku 4.5", "claude-haiku-4-5-20251001"),
}

SYSTEMS = {
    "1": ("Both", ["s", "m"]),
    "2": ("Single agent only", ["s"]),
    "3": ("Multi-agent only", ["m"]),
}

GENRES = {
    "1": ("All four scenes", ["sidescroller", "topdown"]),
    "2": ("Side-scroller only", ["sidescroller"]),
    "3": ("Top-down only", ["topdown"]),
}

# Mean cost per run from 27 Sep 2026. Rough estimate tbh.
OPUS_COST_PER_RUN = {"s": 0.50, "m": 1.20}

# ---------- ASCII ----------

TITLE = r"""
    __  ___      ____  _       ___                    __
   /  |/  /_  __/ / /_(_)     /   | ____ ____  ____  / /_
  / /|_/ / / / / / __/ /_____/ /| |/ __ `/ _ \/ __ \/ __/
 / /  / / /_/ / / /_/ /_____/ ___ / /_/ /  __/ / / / /_
/_/  /_/\__,_/_/\__/_/     /_/  |_\__, /\___/_/ /_/\__/
                                 /____/
   __  __      _ __           ____                  __
  / / / /___  (_) /___  __   / __ )___  ____  _____/ /_
 / / / / __ \/ / __/ / / /  / __  / _ \/ __ \/ ___/ __ \
/ /_/ / / / / / /_/ /_/ /  / /_/ /  __/ / / / /__/ / / /
\____/_/ /_/_/\__/\__, /  /_____/\___/_/ /_/\___/_/ /_/
                 /____/
"""

DIAGRAM = r"""
                     +----------------+
                     |  ORCHESTRATOR  |
                     +-------+--------+
                             |
          +------------------+------------------+
          |                  |                  |
     +----+----+       +-----+----+       +-----+-----+
     |  SCENE  |       |  SCRIPT  |       |  CONSOLE  |
     +----+----+       +-----+----+       +-----+-----+
          |                  |                  |
          +------------------+------------------+
                             |
             ==========[ U N I T Y ]==========

single agent  vs  multi-agent  |  Stephen Wemyss  |  MSc CS, Birkbeck
"""

CYAN, YELLOW, RESET = "\033[96m", "\033[93m", "\033[0m"


def print_banner():
    os.system("")
    print(CYAN + TITLE + RESET)
    print(YELLOW + DIAGRAM + RESET)

# ---------- Prompts ----------

def ask_choice(question: str, options: dict) -> tuple:
    print(f"\n{question}")
    for key, (label, _) in options.items():
        print(f"  {key}) {label}")
    while True:
        answer = input("> ").strip() or "1"
        if answer in options:
            return options[answer]
        print("  Please choose one of the numbers above.")

def ask_int(question: str, default: int, low: int, high: int) -> int:
    while True:
        answer = input(f"\n{question} [{default}]\n> ").strip()
        if not answer:
            return default
        if answer.isdigit() and low <= int(answer) <= high:
            return int(answer)
        print(f"  Please enter a whole number from {low} to {high}.")

def ask_yes_no(question: str, default: bool = True) -> bool:
    hint = "Y/n" if default else "y/N"
    answer = input(f"\n{question} [{hint}]\n> ").strip().lower()
    return default if not answer else answer.startswith("y")

# ---------- Actions ----------

def configure_bench() -> Bench | None:
    """Asks for the batch settings, shows a summary, and returns a ready Bench (or None if cancelled)."""
    model_name, model = ask_choice("Which model?", MODELS)
    systems_name, systems = ask_choice("Which systems?", SYSTEMS)
    genres_name, genres = ask_choice("Which scenes?", GENRES)
    reps = ask_int("How many reps per cell?", default=5, low=1, high=20)

    bench = Bench()
    bench.console_level = logging.CRITICAL
    bench.model = model
    bench.systems = systems
    bench.runs = [run for run in Bench.runs if run[0] in genres]
    bench.reps = reps

    runs_per_system = len(bench.runs) * reps
    print("\n--- Batch summary ---")
    print(f"  Model   : {model_name}")
    print(f"  Systems : {systems_name}")
    print(f"  Scenes  : {genres_name} ({len(bench.runs)} scene(s))")
    print(f"  Reps    : {reps}")
    print(f"  Runs    : {runs_per_system * len(systems)}")
    if model == "claude-opus-5-5":
        estimate = sum(OPUS_COST_PER_RUN[s] * runs_per_system for s in systems)
        print(f"  Est.cost: ~${estimate:.0f} (from pilot averages)")

    return bench if ask_yes_no("Start this batch?") else None

def run_benchmark():
    bench = configure_bench()
    if bench is None:
        print("Cancelled.")
        return
    print(f"\nRunning... results will be written to {bench.results_path}")
    bench.run()
    print("\nBatch complete.")
    if ask_yes_no("Analyse these results now?"):
        print(f"Report written to {write_report([bench.results_path])}")

def analyse_results():
    files = sorted(BenchPaths.results.glob("runs_*.jsonl"), reverse=True)[:9]
    if not files:
        print("\nNo results files found.")
        return
    options = {str(i): (f.name, f) for i, f in enumerate(files, start=1)}
    _, chosen = ask_choice("Which results file? (newest first)", options)
    print(f"Report written to {write_report([chosen])}")

# ---------- Loop ----------

MENU = {
    "1": ("Run the benchmark", run_benchmark),
    "2": ("Analyse a results file", analyse_results),
    "3": ("Quit", None),
}

def main():
    print_banner()
    try:
        while True:
            _, action = ask_choice("What would you like to do?", MENU)
            if action is None:
                break
            action()
    except (KeyboardInterrupt, EOFError):
        print()
    print("Goodbye.")

if __name__ == "__main__":
    main()
