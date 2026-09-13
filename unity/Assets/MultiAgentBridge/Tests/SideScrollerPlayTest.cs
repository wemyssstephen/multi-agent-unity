using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Reflection;

public class SideScrollerPlayTest
{
    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return SceneManager.LoadSceneAsync("current", LoadSceneMode.Single);
    }

    [UnityTest]
    public IEnumerator MoveTest()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null) { Assert.Fail("Player GameObject not found in the scene."); yield break;}

        Component component = player.GetComponent("PlayerMover");
        if (component == null) { Assert.Fail("PlayerMover component not found on Player GameObject."); yield break;}

        MethodInfo move = component.GetType().GetMethod("Move", new[] { typeof(Vector2) });
        if (move == null) { Assert.Fail("Move(Vector2) method not found in PlayerMover component"); yield break; }
        
        yield return PlayerMoveHelper(player, component, move, Vector2.right, "right");
        yield return PlayerMoveHelper(player, component, move, Vector2.left,  "left");
    }

    [UnityTest]
    public IEnumerator PlayerGravityTest()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null) { Assert.Fail("Player GameObject not found in the scene."); yield break; }

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb == null) { Assert.Fail("Side-scroller Player has no Rigidbody2D, so it cannot fall under gravity."); yield break; }

        Assert.Greater(rb.gravityScale, 0f, "Side-scroller Player has a Rigidbody2D but gravity is disabled.");
        yield return null;
    }

    [UnityTest]
    public IEnumerator JumpTest()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null) { Assert.Fail("Player GameObject not found in the scene."); yield break; }

        Component component = player.GetComponent("PlayerMover");
        if (component == null) { Assert.Fail("PlayerMover component not found on Player GameObject."); yield break; }

        MethodInfo move = component.GetType().GetMethod("Jump", new[] { typeof(Vector2) });
        if (move == null) { Assert.Fail("Jump(Vector2) method not found in PlayerMover component"); yield break; }

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

    [UnityTest]
    public IEnumerator WallCollideTest()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null) { Assert.Fail("Player GameObject not found in the scene."); yield break; }

        GameObject wall = GameObject.Find("Wall");
        if (wall == null) { Assert.Fail("Wall GameObject not found in the scene."); yield break; }

        Component component = player.GetComponent("PlayerMover");
        if (component == null) { Assert.Fail("PlayerMover component not found on Player GameObject."); yield break; }

        MethodInfo move = component.GetType().GetMethod("Move", new[] { typeof(Vector2) });
        if (move == null) { Assert.Fail("Move(Vector2) method not found in PlayerMover component."); yield break;}

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

    [UnityTest]
    public IEnumerator HealthComponentTest()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null) { Assert.Fail("Player GameObject not found in the scene."); yield break; }

        GameObject wall = GameObject.Find("Wall");
        if (wall == null) { Assert.Fail("Wall GameObject not found in the scene."); yield break; }

        Component component = player.GetComponent("PlayerMover");
        if (component == null) { Assert.Fail("PlayerMover component not found on Player GameObject."); yield break; }

        MethodInfo move = component.GetType().GetMethod("Move", new[] { typeof(Vector2) });
        if (move == null) { Assert.Fail("Move(Vector2) method not found in PlayerMover component."); yield break; }

        var readHealth = GetPlayerHealth(player);
        if (readHealth == null) { Assert.Fail("Health field or property not found in Player components."); yield break; }
        float startHealth = readHealth();

        // Move the player towards the wall
        for (int i = 0; i < 100; i++)
        {
            yield return new WaitForFixedUpdate(); // Wait for the physics update
            move.Invoke(component, new object[] { Vector2.right });
            if (readHealth() < startHealth) { break; } // Exit early if health has decreased
        }

        Assert.Less(readHealth(), startHealth, "Player health did not decrease after colliding with the wall.");
    }

    [UnityTest]
    public IEnumerator WinConditionTest()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null) { Assert.Fail("Player GameObject not found in the scene."); yield break; }

        Component mover = player.GetComponent("PlayerMover");
        if (mover == null) { Assert.Fail("PlayerMover component not found on Player GameObject."); yield break; }
        
        MethodInfo move = mover.GetType().GetMethod("Move", new[] { typeof(Vector2) });
        if (move == null) { Assert.Fail("Move(Vector2) method not found in PlayerMover component."); yield break; }

        GameObject goal = GameObject.Find("Goal");
        if (goal == null) { Assert.Fail("Goal GameObject not found in the scene."); yield break; }

        GameObject levelManagerObject = GameObject.Find("LevelManager");
        if (levelManagerObject == null) { Assert.Fail("LevelManager GameObject not found in the scene."); yield break; }

        Component levelManager = levelManagerObject.GetComponent("LevelManager");
        if (levelManager == null) { Assert.Fail("LevelManager component not found on LevelManager GameObject."); yield break; }

        var winCondition = GetWinCondition(levelManager);
        if (winCondition == null) { Assert.Fail("winCondition field not found in LevelManager component."); yield break; }

        Vector2 direction = goal.transform.position.x > player.transform.position.x ? Vector2.right : Vector2.left;

        for (int i = 0; i < 200; i++)
        {
            yield return new WaitForFixedUpdate(); // Wait for the physics update
            move.Invoke(mover, new object[] { direction });
            if (winCondition()) break; 
        }

        Assert.IsTrue(winCondition(), "Player did not reach the goal and trigger the win condition.");
    }

    Func<bool> GetWinCondition(Component component)
    {
        var type = component.GetType();
        var field = type.GetField("winCondition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null) { return () => (bool)field.GetValue(component); }

        var property = type.GetProperty("winCondition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null) { return () => (bool)property.GetValue(component); }

        return null;
    }

    Func<float> GetPlayerHealth(GameObject player)
    {
        foreach (var component in player.GetComponents<Component>())
        {
            var type = component.GetType();
            var field = type.GetField("health", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) { return () => Convert.ToSingle(field.GetValue(component)); }

            var property = type.GetProperty("health", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null) { return () => Convert.ToSingle(property.GetValue(component)); }
        }
        return null;
    }

        IEnumerator PlayerMoveHelper(GameObject player, Component component, MethodInfo move,
                                Vector2 direction, string label)
    {
        Vector3 start = player.transform.position;
        var rb = player.GetComponent<Rigidbody2D>();
        for (int i = 0; i < 10; i++)
        {
            yield return new WaitForFixedUpdate();
            move.Invoke(component, new object[] { direction });
            Vector2 travelled = player.transform.position - start;
            if (Vector2.Dot(travelled, direction) > 0.1f) break;
        }

        Vector2 moved = player.transform.position - start;
        if (Vector2.Dot(moved, direction) > 0.1f) { yield break; }

        // Try to find a velocity stomp from a broken Move()
        if (rb != null)
        {
            move.Invoke(component, new object[] { direction });
            float justSet = rb.linearVelocity.magnitude;
            yield return new WaitForFixedUpdate();
            float afterFrame = rb.linearVelocity.magnitude;

            if (justSet > 0.01f && afterFrame < 0.01f)
            {
                Assert.Fail($"Player did not move {label} and Move() seems to be resetting velocity each frame.");
            }
        }
        Assert.Greater(Vector2.Dot(moved, direction), 0.1f, $"Player did not move {label}.");
    }
}