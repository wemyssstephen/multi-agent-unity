import json
import logging
import time

from multi_agent_unity.bridge_manager import post
from multi_agent_unity.config import Bridge
from multi_agent_unity.exceptions import TestPollTimeout

log = logging.getLogger("test_manager")

def run_tests(test_names: list[str]) -> str:
    result = post({"Name": "run_tests", "Args": {"TestNames": test_names}})
    log.info("run_tests -> %s", result)
    return result

def poll_test_result() -> str:
    return post({"Name": "poll_test_result", "Args": {}})

def await_tests():
    for _ in range(Bridge.test_poll_limit):
        state = poll_test_result()
        if state != "running":
            return json.loads(state)
        time.sleep(1)
    raise TestPollTimeout(f"Test suite did not resolve within {Bridge.test_poll_limit}s")
