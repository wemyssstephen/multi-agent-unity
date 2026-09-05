using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using System.Reflection;

public class SideScrollerPlayTest
{
    [UnityTest]

    public IEnumerator MoveTest()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null) { Assert.Fail("Player GameObject not found in the scene.");}

        Component component = player.GetComponent("PlayerMover");
        if (component == null) { Assert.Fail("PlayerMover component not found on Player GameObject.");}

        MethodInfo move = component.GetType().GetMethod("Move", new[] { typeof(Vector2) });
        if (move == null) { Assert.Fail("Move(Vector2) method not found in PlayerMover component");}
        
        Vector3 start = player.transform.position;
        move.Invoke(component, new object[] { Vector2.right });
        yield return null; // Wait for one frame to allow the movement to take effect
        Vector3 end  = player.transform.position;

        Assert.Greater(end.x, start.x, "Player did not move to the right as expected.");
        Assert.AreEqual(start.y, end.y, 0.01f, "Player y drifted.");
    }

    public IEnumerator JumpTest()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null) { Assert.Fail("Player GameObject not found in the scene.");}

        Component component = player.GetComponent("PlayerMover");
        if (component == null) { Assert.Fail("PlayerMover component not found on Player GameObject.");}

        MethodInfo move = component.GetType().GetMethod("Jump", new[] { typeof(Vector2) });
        if (move == null) { Assert.Fail("Jump(Vector2) method not found in PlayerMover component");}

        Vector3 start = player.transform.position;
        float peakY = start.y;
        bool hasJumped = false;
        bool landed = false;

        move.Invoke(component, new object[] { Vector2.up });
        
        for (int i = 0; i < 300; i++)
        {
            yield return new WaitForFixedUpdate(); // Wait for the physics update
            Vector3 currentPosition = player.transform.position;
            if (currentPosition.y > peakY) peakY = currentPosition.y; // Update peakY if the player has jumped higher
            if (currentPosition.y > start.y + 0.1f) hasJumped = true; // Check if the player has jumped
            if (hasJumped && currentPosition.y <= start.y + 0.1f) { landed = true; break; } // Check if the player has landed
        }

        Vector3 end = player.transform.position;

        Assert.IsTrue(hasJumped, "Player did not jump as expected.");
        Assert.IsTrue(landed, "Player did not land back on the ground as expected.");
        Assert.AreEqual(start.x, end.x, 0.01f, "Player x drifted.");
    }

    public IEnumerator WallCollideTest()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null) { Assert.Fail("Player GameObject not found in the scene.");}

        GameObject wall = GameObject.Find("Wall");
        if (wall == null) { Assert.Fail("Wall GameObject not found in the scene.");}

        Component component = player.GetComponent("PlayerMover");
        if (component == null) { Assert.Fail("PlayerMover component not found on Player GameObject.");}

        MethodInfo move = component.GetType().GetMethod("Move", new[] { typeof(Vector2) });
        if (move == null) { Assert.Fail("Move(Vector2) method not found in PlayerMover component.");}

        Vector3 wallPosition = wall.transform.position;
        Vector3 playerStart = player.transform.position;

        // Move the player towards the wall
        for (int i = 0; i < 100; i++)
        {
            yield return new WaitForFixedUpdate(); // Wait for the physics update
            move.Invoke(component, new object[] { Vector2.right });
        }

        Vector3 playerEnd = player.transform.position;
        Assert.Greater(wallPosition.x, playerStart.x + 2f, "Wall is not positioned to the right of the player — test setup issue.");
        Assert.Greater(playerEnd.x, playerStart.x, "Player did not move toward the wall — Move produced no motion.");
        Assert.LessOrEqual(playerEnd.x, wallPosition.x - 0.5f, "Player passed through the wall, collision detection failed.");
    }
}