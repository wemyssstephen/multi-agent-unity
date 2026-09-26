import logging
import time

import requests

from multi_agent_unity.config import Bridge
from multi_agent_unity.exceptions import BridgeTimeout

log = logging.getLogger("bridge")
_session = requests.Session()

def post(payload: dict) -> str:
    for _ in range(Bridge.connect_retries):
        try:
            response = _session.post(Bridge.url, json=payload, timeout=Bridge.timeout)
            return response.text
        except (requests.exceptions.Timeout, TimeoutError) as e:
            raise BridgeTimeout("post timed out") from e
        except requests.exceptions.ConnectionError:
            time.sleep(1)
    return "Unity bridge unreachable after retrying."

def post_with_compile_retry(payload: dict) -> str:
    for _ in range(Bridge.post_retries):
        try:
            result = post(payload)
        except requests.exceptions.ConnectionError:
            time.sleep(1)
            continue
        if "Failed to find component type" not in result:
            return result
        time.sleep(1)
    return result

def post_quit(payload: dict) -> None:
    try:
        _session.post(Bridge.url, json=payload, timeout=Bridge.quit_timeout)
    except (requests.exceptions.ConnectionError, requests.exceptions.Timeout):
        pass
