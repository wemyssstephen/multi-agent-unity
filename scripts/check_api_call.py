
import asyncio

from multi_agent_unity.agent import agent_loop

if __name__ == "__main__":
    result = asyncio.run(agent_loop("Build a snowman out of three white spheres."))
    print(result)
