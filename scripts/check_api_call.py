from multi_agent_unity.agent import agent_loop

if __name__ == "__main__":
    response = agent_loop("Give me a haiku about a silly cat.")
    print(f"Agent Response:\n {response['text']}")
    print(f"Total Input Tokens: {response['input_tokens']}")
    print(f"Total Output Tokens: {response['output_tokens']}")
