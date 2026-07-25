import os
from dotenv import load_dotenv

# Get the path to the .env file and load
env_path = os.path.join(os.path.dirname(__file__), '.env')
load_dotenv(dotenv_path=env_path)

def get_anthropic_api_key() -> str:
    """
    Retrieves the Anthropic API key from the environment variables.

    Returns:
        str: The Anthropic API key.
    """
    api_key = os.getenv("ANTHROPIC_API_KEY")
    if not api_key:
        raise RuntimeError("ANTHROPIC_API_KEY is not set in the .env file.")
    return api_key
