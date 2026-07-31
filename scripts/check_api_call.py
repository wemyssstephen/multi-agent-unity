from fake_tools import TOOL_DEFINITIONS, TOOL_REGISTRY

from multi_agent_unity.agent import agent_loop

if __name__ == "__main__":
    response = agent_loop("Please add 3 and 5 together.", TOOL_REGISTRY, TOOL_DEFINITIONS)
    print(f"Agent Response:\n {response['text']}")
    print(f"Total Input Tokens: {response['input_tokens']}")
    print(f"Total Output Tokens: {response['output_tokens']}")
