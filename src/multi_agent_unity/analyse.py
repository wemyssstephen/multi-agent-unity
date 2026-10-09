import csv
import json
import statistics
import sys
from math import comb
from pathlib import Path

from tabulate import tabulate

# Claude Opus 5.5 list prices 27 Sep 2026
PRICES = {
    "input_tokens": 4.00,
    "output_tokens": 20.00,
    "cache_creation_input_tokens": 5.00,
    "cache_read_input_tokens": 0.20,
}

SYSTEMS = {"s": "single agent", "m": "multi-agent"}


def load_runs(files):
    """Reads every row from one or more runs files."""
    runs = []
    for file in files:
        for line in Path(file).read_text(encoding="utf-8").splitlines():
            if line.strip():
                run = json.loads(line)
                run["batch"] = Path(file)
                runs.append(run)
    return runs

def pass_at_k(n, c, k):
    """Chance at least one of k runs passes, given c passes out of n."""
    if n - c < k:
        return 1.0
    return 1 - comb(n - c, k) / comb(n, k)

def pass_hat_k(n, c, k):
    """Chance all k runs pass, given c passes out of n."""
    return comb(c, k) / comb(n, k)

def cost(run):
    total = 0
    for field, price in PRICES.items():
        total += run.get(field, 0) * price
    return total / 1_000_000

def mean_and_sd(values):
    if len(values) < 2:
        return f"{values[0]:.2f}" if values else "-"
    return f"{statistics.mean(values):.2f} ± {statistics.stdev(values):.2f}"

def count(counts, key):
    counts[key] = counts.get(key, 0) + 1

# ---------- Tables ----------

def capability_table(runs):
    headers = ["Scene", "System", "n", "Passes", "Compiled",
               "Pass@1", "Pass@5", "Pass@10", "Pass^1", "Pass^5", "Pass^10"]
    rows = []
    for scene in sorted(set(run["scene"] for run in runs)):
        for system in SYSTEMS:
            cell = [run for run in runs if run["scene"] == scene and run["system"] == system]
            if not cell:
                continue
            n = len(cell)
            c = len([run for run in cell if run["passed"]])
            compiled = len([run for run in cell if run["compiled"]])
            row = [scene, SYSTEMS[system], n, c, compiled]
            for k in [1, 5, 10]:
                row.append(round(pass_at_k(n, c, k), 2) if k <= n else "-")
            for k in [1, 5, 10]:
                row.append(round(pass_hat_k(n, c, k), 2) if k <= n else "-")
            rows.append(row)
    return headers, rows

def test_table(runs):
    passed = {}
    total = {}
    for run in runs:
        for test, state in run.get("results", {}).items():
            key = (test.split(".")[-1], run["system"])
            count(total, key)
            if state == "Passed":
                count(passed, key)

    headers = ["Test", SYSTEMS["s"], SYSTEMS["m"]]
    rows = []
    for test in sorted(set(name for name, _ in total)):
        row = [test]
        for system in SYSTEMS:
            key = (test, system)
            row.append(f"{passed.get(key, 0)}/{total[key]}" if key in total else "-")
        rows.append(row)
    return headers, rows

def cost_table(runs):
    headers = ["System", "n", "Cost (USD)", "Model calls", "Tool calls",
               "Output tokens", "Hit call limit"]
    rows = []
    for system in SYSTEMS:
        cell = [run for run in runs if run["system"] == system]
        if not cell:
            continue
        rows.append([
            SYSTEMS[system],
            len(cell),
            mean_and_sd([cost(run) for run in cell]),
            mean_and_sd([run.get("iterations", 0) for run in cell]),
            mean_and_sd([run.get("tool_call_count", 0) for run in cell]),
            mean_and_sd([run.get("output_tokens", 0) for run in cell]),
            len([run for run in cell if run.get("hit_cap")]),
        ])
    return headers, rows

def failure_table(runs, errors):
    counts = {}
    for run in errors:
        count(counts, (SYSTEMS[run["system"]], "harness error", run.get("reason", "unknown")))
    for run in runs:
        if not run["compiled"]:
            count(counts, (SYSTEMS[run["system"]], "did not compile", ""))
        for test, message in run.get("messages", {}).items():
            first_line = message.strip().splitlines()[0] if message.strip() else ""
            count(counts, (SYSTEMS[run["system"]], test.split(".")[-1], first_line))

    headers = ["System", "Failure", "Detail", "Count"]
    rows = [[system, failure, detail, n] for (system, failure, detail), n in sorted(counts.items())]
    return headers, rows

# ---------- Output ----------

def save_table(folder, name, headers, rows):
    """Prints a table to the terminal and saves it as a CSV file."""
    print(f"\n{name}")
    print(tabulate(rows, headers=headers))
    with open(folder / f"{name}.csv", "w", newline="", encoding="utf-8") as file:
        writer = csv.writer(file)
        writer.writerow(headers)
        writer.writerows(rows)

def write_report(files):
    """Analyses the runs files and saves each table as a CSV. Returns the output folder."""
    all_runs = load_runs(files)
    runs = [run for run in all_runs if not run.get("error")]
    errors = [run for run in all_runs if run.get("error")]

    first = Path(files[0])
    folder = first.parent / first.stem.replace("runs_", "analysis_")
    folder.mkdir(exist_ok=True)

    print(f"{len(all_runs)} rows: {len(runs)} valid runs, {len(errors)} harness errors (excluded)")
    save_table(folder, "capability", *capability_table(runs))
    save_table(folder, "tests", *test_table(runs))
    save_table(folder, "cost", *cost_table(runs))
    save_table(folder, "failures", *failure_table(runs, errors))
    return folder

if __name__ == "__main__":
    print(f"\nTables saved to {write_report(sys.argv[1:])}")
