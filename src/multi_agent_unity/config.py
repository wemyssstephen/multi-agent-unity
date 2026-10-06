import os
from pathlib import Path

from dotenv import load_dotenv

load_dotenv()

REPO_ROOT = Path(__file__).resolve().parents[2]

class BenchPaths:
    """Filesystem paths used by Python."""
    unity_project = REPO_ROOT / "unity"
    results = REPO_ROOT / "results"
    unity_log = REPO_ROOT / "unity_headless.log"
    working_dir = unity_project / "Assets/MultiAgentBridge/Working"
    seed_scripts = unity_project / "Scenes/SceneScripts"
    working_scripts = working_dir / "Scripts"

    unity_exe = Path(os.getenv(
        "UNITY_EXE", r"C:/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor/Unity.exe"))


class UnityAssets:
    """Asset paths relative to the Unity project."""
    working_scene = "Assets/MultiAgentBridge/Working/current.unity"
    scene_templates = "Assets/MultiAgentBridge/Scenes"


class Bridge:
    url = "http://localhost:8080"
    timeout = 30            # seconds to wait for one bridge reply
    connect_retries = 30    # attempts while the bridge is down mid-reload
    post_retries = 10
    compile_wait = 30       # seconds to wait for a compile to finish
    startup_wait = 120      # seconds to wait for a fresh Unity to answer
    test_poll_limit = 90    # seconds a test suite may stay "running"
    quit_timeout = 5        # seconds to wait when exiting Unity


class AnthropicAPI:
    endpoint = "https://api.anthropic.com/v1/messages"
    version = "2023-06-01"

    max_tokens = 32000
    timeout = 600
    retries = 5
    RETRYABLE = {429, 500, 502, 503, 529}

    @staticmethod
    def api_key() -> str:
        api_key = os.getenv("ANTHROPIC_API_KEY")
        if not api_key:
            raise RuntimeError("ANTHROPIC_API_KEY is not set in the .env file.")
        return api_key
