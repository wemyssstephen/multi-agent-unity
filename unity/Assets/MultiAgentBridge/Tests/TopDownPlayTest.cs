using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Reflection;

public class TopDownPlayTest
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
        if (player == null) { Assert.Fail("Player GameObject not found in the scene."); yield break; }

        Component component = player.GetComponent("PlayerMover");
        if (component == null) { Assert.Fail("PlayerMover component not found on Player GameObject."); yield break; }

        MethodInfo move = component.GetType().GetMethod("Move", new[] { typeof(Vector2) });
        if (move == null) { Assert.Fail("Move(Vector2) method not found in PlayerMover component"); yield break; }

        yield return PlayerMoveHelper(player, component, move, Vector2.right, "right");
        yield return PlayerMoveHelper(player, component, move, Vector2.left,  "left");
        yield return PlayerMoveHelper(player, component, move, Vector2.up,    "up");
        yield return PlayerMoveHelper(player, component, move, Vector2.down,  "down");
    }

    [UnityTest]
    public IEnumerator PlayerGravityTest()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null) { Assert.Fail("Player GameObject not found in the scene."); yield break; }

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            Assert.AreEqual(0f, rb.gravityScale, "Top-down Player has a Rigidbody2D with gravity enabled; it will fall.");
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator GroundIsBackgroundTest()
    {
        GameObject ground = GameObject.Find("Ground");
        if (ground == null) { Assert.Fail("Ground GameObject not found in the scene."); yield break; }

        Collider2D collider = ground.GetComponent<Collider2D>();
        Assert.IsNull(collider, "Ground should be background only and have no collider, but a Collider2D was found.");
        yield return null;
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
        if (move == null) { Assert.Fail("Move(Vector2) method not found in PlayerMover component."); yield break; }

        Vector3 wallPosition = wall.transform.position;
        Vector3 playerStart = player.transform.position;

        for (int i = 0; i < 100; i++)
        {
            yield return new WaitForFixedUpdate();
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

        for (int i = 0; i < 100; i++)
        {
            yield return new WaitForFixedUpdate();
            move.Invoke(component, new object[] { Vector2.right });
            if (readHealth() < startHealth) { break; }
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

        Vector3 toGoal = goal.transform.position - player.transform.position;
        Vector2 direction = new Vector2(toGoal.x, toGoal.y).normalized;

        for (int i = 0; i < 200; i++)
        {
            yield return new WaitForFixedUpdate();
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
        for (int i = 0; i < 10; i++)
        {
            yield return new WaitForFixedUpdate();
            move.Invoke(component, new object[] { direction });
            Vector2 travelled = player.transform.position - start;
            if (Vector2.Dot(travelled, direction) > 0.1f) break;
        }

        Vector2 moved = player.transform.position - start;
        Assert.Greater(Vector2.Dot(moved, direction), 0f, $"Player did not move {label}.");
    }

}