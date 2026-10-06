using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Reflection;

using static MultiAgentBridge.PlayTestHelpers;

public class SideScrollerPlayTest
{
    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return SceneManager.LoadSceneAsync("current", LoadSceneMode.Single);
        LogAssert.ignoreFailingMessages = true;
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

        MethodInfo jump = component.GetType().GetMethod("Jump", new[] { typeof(Vector2) });
        if (jump == null) { Assert.Fail("Jump(Vector2) method not found in PlayerMover component"); yield break; }

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb == null) { Assert.Fail("Player has no Rigidbody2D."); yield break; }

        // Step 1: let the Player land and come to rest.
        for (int i = 0; i < 100; i++)
        {
            yield return new WaitForFixedUpdate();
        }
        Assert.Less(Mathf.Abs(rb.linearVelocity.y), 0.01f, "Player never came to rest on the ground.");

        // Step 2: jump, and track the highest point reached.
        float startY = player.transform.position.y;
        float highestY = startY;

        jump.Invoke(component, new object[] { Vector2.up });

        for (int i = 0; i < 300; i++)
        {
            yield return new WaitForFixedUpdate();
            highestY = Mathf.Max(highestY, player.transform.position.y);
        }
        float endY = player.transform.position.y;

        // Step 3: check it went up, then came back down.
        Assert.Greater(highestY, startY + 0.5f, "Player did not jump.");
        Assert.Less(endY, highestY - 0.3f, "Player jumped but never came back down.");
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

        // Isolate movement from the idle input driver by disabling every Player MonoBehaviour
        DisablePlayerScripts(player, keepAlive: null);

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

        Component healthComp = FindHealthComponent(player);
        if (healthComp == null) { Assert.Fail("Health field or property not found in Player components."); yield break; }
        Func<float> readHealth = ReadHealthFrom(healthComp);
        float startHealth = readHealth();

        // Isolate movement from the idle input driver
        DisablePlayerScripts(player, keepAlive: healthComp);

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

        GameObject goal = GameObject.Find("Goal");
        if (goal == null) { Assert.Fail("Goal GameObject not found in the scene."); yield break; }

        GameObject levelManagerObject = GameObject.Find("LevelManager");
        if (levelManagerObject == null) { Assert.Fail("LevelManager GameObject not found in the scene."); yield break; }

        Component levelManager = levelManagerObject.GetComponent("LevelManager");
        if (levelManager == null) { Assert.Fail("LevelManager component not found on LevelManager GameObject."); yield break; }

        Func<bool> winCondition = GetWinCondition(levelManager);
        if (winCondition == null) { Assert.Fail("winCondition field not found in LevelManager component."); yield break; }

        // Teleport the player onto the goal
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        Vector3 goalPos = goal.transform.position;

        for (int i = 0; i < 20; i++)
        {
            player.transform.position = goalPos;
            if (rb != null) { rb.position = goalPos; rb.linearVelocity = Vector2.zero; }
            yield return new WaitForFixedUpdate();
            if (winCondition()) break;
        }

        Assert.IsTrue(winCondition(), "Player did not reach the goal and trigger the win condition.");
    }

    // ---------- Enemy tests ----------
    

     [UnityTest]
    public IEnumerator EnemyPatrolTest()
    {
        GameObject enemy = GameObject.Find("Enemy");
        if (enemy == null) { Assert.Fail("Enemy GameObject not found in the scene."); yield break; }
 
        // Let the scene settle first (e.g. a side-scroller Enemy landing on the Ground).
        for (int i = 0; i < 50; i++)
        {
            yield return new WaitForFixedUpdate();
        }
        if (IsGone(enemy)) { Assert.Fail("Enemy was destroyed before it could patrol."); yield break; }
 
        // Watch for 3 seconds and record the furthest it gets from where it started.
        Vector2 start = enemy.transform.position;
        float furthest = 0f;
        for (int i = 0; i < 150; i++)
        {
            yield return new WaitForFixedUpdate();
            if (IsGone(enemy)) { break; }
            Vector2 now = enemy.transform.position;
            furthest = Mathf.Max(furthest, Vector2.Distance(start, now));
        }
 
        Assert.Greater(furthest, 0.5f, "Enemy did not patrol.");
    }
 
    [UnityTest]
    public IEnumerator EnemyContactTest()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null) { Assert.Fail("Player GameObject not found in the scene."); yield break; }
 
        GameObject enemy = GameObject.Find("Enemy");
        if (enemy == null) { Assert.Fail("Enemy GameObject not found in the scene."); yield break; }
 
        Component healthComp = FindHealthComponent(player);
        if (healthComp == null) { Assert.Fail("Health field or property not found in Player components."); yield break; }
        Func<float> readHealth = ReadHealthFrom(healthComp);
        float startHealth = readHealth();
 
        // Hold the Player on the Enemy until contact registers.
        for (int i = 0; i < 50; i++)
        {
            if (IsGone(enemy)) { break; }
            Teleport(player, enemy.transform.position);
            yield return new WaitForFixedUpdate();
            if (readHealth() < startHealth) { break; }
        }
 
        Assert.Less(readHealth(), startHealth, "Player health did not decrease after touching the Enemy.");
    }
 
    [UnityTest]
    public IEnumerator AttackTest()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null) { Assert.Fail("Player GameObject not found in the scene."); yield break; }
 
        Component mover = player.GetComponent("PlayerMover");
        if (mover == null) { Assert.Fail("PlayerMover component not found on Player GameObject."); yield break; }
 
        MethodInfo attack = mover.GetType().GetMethod("Attack", Type.EmptyTypes);
        if (attack == null) { Assert.Fail("Attack() method not found in PlayerMover component."); yield break; }
 
        GameObject enemy = GameObject.Find("Enemy");
        if (enemy == null) { Assert.Fail("Enemy GameObject not found in the scene."); yield break; }
 
        Component enemyComp = enemy.GetComponent("Enemy");
        if (enemyComp == null) { Assert.Fail("Enemy component not found on Enemy GameObject."); yield break; }
 
        Func<float> readEnemyHealth = ReadHealthFrom(enemyComp);
        if (readEnemyHealth == null) { Assert.Fail("Health field not found in Enemy component."); yield break; }
        float startHealth = readEnemyHealth();
 
        bool damaged = false;
        for (int attempt = 0; attempt < 6 && !damaged; attempt++)
        {
            // Stand 1 unit to the left of the Enemy for half a second (also covers any attack cooldown).
            for (int i = 0; i < 25; i++)
            {
                if (IsGone(enemy)) { break; }
                Teleport(player, enemy.transform.position + Vector3.left);
                yield return new WaitForFixedUpdate();
            }
            if (IsGone(enemy)) { Assert.Fail("Enemy disappeared before it was attacked."); yield break; }
 
            attack.Invoke(mover, null);
            yield return new WaitForFixedUpdate();
            damaged = IsGone(enemy) || readEnemyHealth() < startHealth;
        }
 
        Assert.IsTrue(damaged, "Attack() did not damage the Enemy.");
    }
 
    [UnityTest]
    public IEnumerator GatedWinTest()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null) { Assert.Fail("Player GameObject not found in the scene."); yield break; }
 
        GameObject goal = GameObject.Find("Goal");
        if (goal == null) { Assert.Fail("Goal GameObject not found in the scene."); yield break; }
 
        GameObject levelManagerObject = GameObject.Find("LevelManager");
        if (levelManagerObject == null) { Assert.Fail("LevelManager GameObject not found in the scene."); yield break; }
 
        Component levelManager = levelManagerObject.GetComponent("LevelManager");
        if (levelManager == null) { Assert.Fail("LevelManager component not found on LevelManager GameObject."); yield break; }
 
        Func<bool> winCondition = GetWinCondition(levelManager);
        if (winCondition == null) { Assert.Fail("winCondition field not found in LevelManager component."); yield break; }
 
        GameObject enemy = GameObject.Find("Enemy");
        if (enemy == null) { Assert.Fail("Enemy GameObject not found in the scene."); yield break; }
 
        Component enemyComp = enemy.GetComponent("Enemy");
        if (enemyComp == null) { Assert.Fail("Enemy component not found on Enemy GameObject."); yield break; }
 
        MethodInfo takeDamage = GetTakeDamage(enemyComp.GetType());
        if (takeDamage == null) { Assert.Fail("TakeDamage(int) method not found in Enemy component."); yield break; }
 
        Vector3 away = player.transform.position;
        Vector3 goalPos = goal.transform.position;
 
        // 1. Reaching the Goal while the Enemy is alive must NOT win.
        for (int i = 0; i < 20; i++)
        {
            Teleport(player, goalPos);
            yield return new WaitForFixedUpdate();
        }
        Assert.IsFalse(winCondition(), "Win triggered while the Enemy was still alive.");
 
        // Step off the Goal, so reaching it again fires its trigger again.
        for (int i = 0; i < 5; i++)
        {
            Teleport(player, away);
            yield return new WaitForFixedUpdate();
        }
 
        // 2. Defeat the Enemy directly (independent of Attack), and check it is destroyed.
        InvokeTakeDamage(takeDamage, enemyComp, 9999);
        for (int i = 0; i < 10 && !IsGone(enemy); i++)
        {
            yield return new WaitForFixedUpdate();
        }
        Assert.IsTrue(IsGone(enemy), "Enemy was not destroyed when its health reached zero.");
 
        // 3. Now reaching the Goal must win.
        for (int i = 0; i < 20; i++)
        {
            Teleport(player, goalPos);
            yield return new WaitForFixedUpdate();
            if (winCondition()) { break; }
        }
        Assert.IsTrue(winCondition(), "Player did not win after defeating the Enemy and reaching the Goal.");
    }
 
    // ---------- HUD ----------
 
    [UnityTest]
    public IEnumerator HealthBarTest()
    {
        GameObject bar = GameObject.Find("HealthBar");
        if (bar == null) { Assert.Fail("HealthBar GameObject not found in the scene."); yield break; }
 
        Component slider = bar.GetComponent("Slider");
        if (slider == null) { Assert.Fail("HealthBar has no Slider component."); yield break; }
 
        if (bar.GetComponentInParent<Canvas>() == null) { Assert.Fail("HealthBar is not inside a Canvas."); yield break; }
        if (!AnchoredTopLeft(bar)) { Assert.Fail("HealthBar is not anchored to the top-left of the screen."); yield break; }
 
        PropertyInfo valueProperty = slider.GetType().GetProperty("value");
        Func<float> readBar = () => Convert.ToSingle(valueProperty.GetValue(slider));
 
        GameObject player = GameObject.Find("Player");
        if (player == null) { Assert.Fail("Player GameObject not found in the scene."); yield break; }
 
        Component mover = player.GetComponent("PlayerMover");
        if (mover == null) { Assert.Fail("PlayerMover component not found on Player GameObject."); yield break; }
 
        MethodInfo move = mover.GetType().GetMethod("Move", new[] { typeof(Vector2) });
        if (move == null) { Assert.Fail("Move(Vector2) method not found in PlayerMover component."); yield break; }
 
        Component healthComp = FindHealthComponent(player);
        if (healthComp == null) { Assert.Fail("Health field or property not found in Player components."); yield break; }
        Func<float> readHealth = ReadHealthFrom(healthComp);
 
        // 1. The bar starts in sync with the Player's health.
        yield return null;
        yield return null;
        Assert.AreEqual(readHealth(), readBar(), 0.01f, "HealthBar does not show the Player's starting health.");
 
        // 2. Damage the Player on the Wall, the same way as HealthComponentTest.
        float startHealth = readHealth();
        DisablePlayerScripts(player, keepAlive: healthComp);
        for (int i = 0; i < 100; i++)
        {
            yield return new WaitForFixedUpdate();
            move.Invoke(mover, new object[] { Vector2.right });
            if (readHealth() < startHealth) { break; }
        }
        if (readHealth() >= startHealth) { Assert.Fail("Could not damage the Player on the Wall; HealthBar tracking untested."); yield break; }
 
        // Stop the Player so it doesn't keep hitting the Wall, then let the HUD update.
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.linearVelocity = Vector2.zero; }
        for (int i = 0; i < 5; i++)
        {
            yield return null;
        }
 
        // 3. The bar now shows the new health.
        Assert.AreEqual(readHealth(), readBar(), 0.01f, "HealthBar did not update when the Player took damage.");
    }
}