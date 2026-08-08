
import asyncio

from multi_agent_unity.agent import agent_loop

if __name__ == "__main__":
    result = asyncio.run(agent_loop("Create a cube named hello_world."))
    print(result)
