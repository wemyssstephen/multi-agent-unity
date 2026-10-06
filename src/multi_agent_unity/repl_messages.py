from multi_agent_unity.analyse import cost

GREEN = "\033[92m"
RED = "\033[91m"
YELLOW = "\033[93m"
CYAN = "\033[96m"
RESET = "\033[0m"

SYSTEM_NAMES = {"s": "single-agent", "m": "multi-agent"}


def message(text):
    print(CYAN + text + RESET)


def cell_started(number, total, genre, condition, system, rep):
    print(f"[{number}/{total}] Running {SYSTEM_NAMES[system]} {genre} ({condition}) rep {rep}...")


def cell_finished(number, total, genre, condition, system, rep, record, seconds):
    if record.get("error"):
        outcome = YELLOW + "ERROR " + str(record.get("reason")) + RESET
    elif not record.get("compiled"):
        outcome = RED + "did not compile" + RESET
    else:
        results = record["results"]
        passed = len([state for state in results.values() if state == "Passed"])
        if record["passed"]:
            outcome = GREEN + f"{passed}/{len(results)} PASS" + RESET
        else:
            outcome = RED + f"{passed}/{len(results)} FAIL" + RESET

    run_cost = cost(record) if "output_tokens" in record else 0
    minutes = int(seconds // 60)
    print(f"[{number}/{total}] {SYSTEM_NAMES[system]} {genre} ({condition}) rep {rep}: "
          f"{outcome}, ${run_cost:.2f}, {minutes} min")