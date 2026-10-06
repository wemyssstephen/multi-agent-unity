using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Reflection;

using static MultiAgentBridge.PlayTestHelpers;

public class TopDownPlayTest
{
    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return SceneManager.LoadSceneAsync("current", LoadSceneMode.Single);
        LogAssert.ignoreFailingMessages = true;

        // Let Awake and Start run, and the scene settle, before any test acts.
        for (int i = 0; i < 10; i++)
        {
            yield return new WaitForFixedUpdate();
        }
    }

    [UnityTest]
    public IEnumerator MoveTest()
    {
        GameObject player = FindOrFail("Player");
        Component mover = ComponentOrFail(player, "PlayerMover");
        MethodInfo move = MethodOrFail(mover, "Move", typeof(Vector2));

        yield return PlayerMoveHelper(player, mover, move, Vector2.right, "right");
        yield return PlayerMoveHelper(player, mover, move, Vector2.left,  "left");
        yield return PlayerMoveHelper(player, mover, move, Vector2.up,    "up");
        yield return PlayerMoveHelper(player, mover, move, Vector2.down,  "down");
    }

    [UnityTest]
    public IEnumerator PlayerGravityTest()
    {
        GameObject player = FindOrFail("Player");
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
        GameObject ground = FindOrFail("Ground");
        Collider2D collider = ground.GetComponent<Collider2D>();
        Assert.IsNull(collider, "Ground should be background only and have no collider, but a Collider2D was found.");
        yield return null;
    }


    [UnityTest]
    public IEnumerator WallCollideTest()
    {
        GameObject player = FindOrFail("Player");
        GameObject wall = FindOrFail("Wall");
        Component mover = ComponentOrFail(player, "PlayerMover");
        MethodInfo move = MethodOrFail(mover, "Move", typeof(Vector2));

        Vector3 wallPosition = wall.transform.position;
        Vector3 playerStart = player.transform.position;
        Assert.Less(wallPosition.x, playerStart.x - 1f, "Wall is not positioned to the left of the Player.");

        // Disable every Player script, so input code cannot cancel Move.
        DisablePlayerScripts(player, keepAlive: null);

        for (int i = 0; i < 100; i++)
        {
            move.Invoke(mover, new object[] { Vector2.left });
            yield return new WaitForFixedUpdate();
        }

        Vector3 playerEnd = player.transform.position;
        Assert.Less(playerEnd.x, playerStart.x, "Player did not move toward the Wall. Move produced no motion.");
        Assert.Greater(playerEnd.x, wallPosition.x, "Player passed through the Wall, collision detection failed.");
    }


    [UnityTest]
    public IEnumerator EnemyPatrolTest()
    {
        GameObject enemy = FindOrFail("Enemy");

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
        GameObject player = FindOrFail("Player");
        GameObject enemy = FindOrFail("Enemy");

        Component healthComp = FindHealthComponent(player);
        if (healthComp == null) { Assert.Fail("Health field or property not found in Player components."); }
        Func<float> readHealth = ReadHealthFrom(healthComp);
        float startHealth = readHealth();

        // Hold the Player on the Enemy until contact registers
        for (int i = 0; i < 100; i++)
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
        GameObject player = FindOrFail("Player");
        Component mover = ComponentOrFail(player, "PlayerMover");
        MethodInfo attack = MethodOrFail(mover, "Attack");
        GameObject enemy = FindOrFail("Enemy");
        Component enemyComp = ComponentOrFail(enemy, "Enemy");

        Func<float> readEnemyHealth = ReadHealthFrom(enemyComp);
        if (readEnemyHealth == null) { Assert.Fail("Health field not found in Enemy component."); }
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
            if (IsGone(enemy)) { Assert.Fail("Enemy disappeared before it was attacked."); }

            attack.Invoke(mover, null);
            yield return new WaitForFixedUpdate();
            damaged = IsGone(enemy) || readEnemyHealth() < startHealth;
        }

        Assert.IsTrue(damaged, "Attack() did not damage the Enemy.");
    }

    [UnityTest]
    public IEnumerator HealthBarTest()
    {
        GameObject bar = FindOrFail("HealthBar");
        Component slider = ComponentOrFail(bar, "Slider");
        if (bar.GetComponentInParent<Canvas>() == null) { Assert.Fail("HealthBar is not inside a Canvas."); }
        if (!AnchoredTopLeft(bar)) { Assert.Fail("HealthBar is not anchored to the top-left of the screen."); }

        PropertyInfo valueProperty = slider.GetType().GetProperty("value");
        Func<float> readBar = () => Convert.ToSingle(valueProperty.GetValue(slider));

        GameObject player = FindOrFail("Player");
        GameObject enemy = FindOrFail("Enemy");
        Component healthComp = FindHealthComponent(player);
        if (healthComp == null) { Assert.Fail("Health field or property not found in Player components."); }
        Func<float> readHealth = ReadHealthFrom(healthComp);

        // 1. The bar starts in sync with the Player's health.
        Assert.AreEqual(readHealth(), readBar(), 0.01f, "HealthBar does not show the Player's starting health.");

        // 2. Damage the Player on the Enemy, the same way as EnemyContactTest.
        Vector3 away = player.transform.position;
        float startHealth = readHealth();
        for (int i = 0; i < 100; i++)
        {
            if (IsGone(enemy)) { break; }
            Teleport(player, enemy.transform.position);
            yield return new WaitForFixedUpdate();
            if (readHealth() < startHealth) { break; }
        }
        if (readHealth() >= startHealth) { Assert.Fail("Could not damage the Player on the Enemy; HealthBar tracking untested."); }

        // Move the Player away so it takes no more damage, then let the HUD update.
        yield return HoldPlayerAt(player, away, 5);
        for (int i = 0; i < 5; i++)
        {
            yield return null;
        }

        // 3. The bar now shows the new health.
        Assert.AreEqual(readHealth(), readBar(), 0.01f, "HealthBar did not update when the Player took damage.");
    }

    [UnityTest]
    public IEnumerator DialogueTest()
    {
        GameObject player = FindOrFail("Player");
        GameObject npc = FindOrFail("NPC");
        Component npcComp = ComponentOrFail(npc, "NPC");
        MethodInfo interact = MethodOrFail(npcComp, "Interact");
        MethodInfo advance = MethodOrFail(npcComp, "Advance");

        Component dialogueOwner = FindComponentWithMember(npc, "dialogueFinished");
        if (dialogueOwner == null) { Assert.Fail("dialogueFinished field not found on the NPC."); }

        // A closed dialogue panel is usually inactive, so search inactive objects too.
        GameObject box = FindIncludingInactive("DialogueBox");
        if (box == null) { Assert.Fail("DialogueBox not found in the scene."); }

        // Stand beside the NPC and talk to it
        Vector3 besideNpc = npc.transform.position + Vector3.left;
        yield return HoldPlayerAt(player, besideNpc, 10);
        interact.Invoke(npcComp, null);
        yield return HoldPlayerAt(player, besideNpc, 5);

        Assert.IsTrue(box.activeInHierarchy, "DialogueBox did not open after Interact().");
        string firstLine = ReadText(box);
        Assert.IsNotEmpty(firstLine, "DialogueBox opened but shows no text.");

        // Advance until the dialogue finishes
        bool lineChanged = false;
        for (int i = 0; i < 20 && !ReadBool(dialogueOwner, "dialogueFinished"); i++)
        {
            advance.Invoke(npcComp, null);
            yield return HoldPlayerAt(player, besideNpc, 2);
            if (box.activeInHierarchy && ReadText(box) != firstLine) { lineChanged = true; }
        }

        Assert.IsTrue(ReadBool(dialogueOwner, "dialogueFinished"), "dialogueFinished did not become true after advancing through the dialogue.");
        Assert.IsTrue(lineChanged, "Advance() did not move the dialogue to a new line.");
    }

    [UnityTest]
    public IEnumerator QuestTest()
    {
        GameObject player = FindOrFail("Player");
        GameObject npc = FindOrFail("NPC");
        Component npcComp = ComponentOrFail(npc, "NPC");
        MethodInfo interact = MethodOrFail(npcComp, "Interact");
        MethodInfo advance = MethodOrFail(npcComp, "Advance");
        GameObject enemy = FindOrFail("Enemy");
        Component enemyComp = ComponentOrFail(enemy, "Enemy");
        MethodInfo takeDamage = GetTakeDamage(enemyComp.GetType());
        if (takeDamage == null) { Assert.Fail("TakeDamage(int) method not found in Enemy component."); }

        Component dialogueOwner = FindComponentWithMember(npc, "dialogueFinished");
        if (dialogueOwner == null) { Assert.Fail("dialogueFinished field not found on the NPC."); }
        Component keyOwner = FindComponentWithMember(player, "hasKey");
        if (keyOwner == null) { Assert.Fail("hasKey field not found in Player components."); }
        Assert.IsFalse(ReadBool(keyOwner, "hasKey"), "Player has the key before the quest started.");

        Vector3 besideNpc = npc.transform.position + Vector3.left;

        // Finish the NPC's dialogue.
        yield return HoldPlayerAt(player, besideNpc, 10);
        interact.Invoke(npcComp, null);
        yield return HoldPlayerAt(player, besideNpc, 5);
        for (int i = 0; i < 20 && !ReadBool(dialogueOwner, "dialogueFinished"); i++)
        {
            advance.Invoke(npcComp, null);
            yield return HoldPlayerAt(player, besideNpc, 2);
        }
        if (!ReadBool(dialogueOwner, "dialogueFinished")) { Assert.Fail("Could not finish the NPC dialogue; quest untested."); }

        // Defeat the Enemy directly, independent of Attack.
        InvokeTakeDamage(takeDamage, enemyComp, 9999);
        for (int i = 0; i < 10 && !IsGone(enemy); i++)
        {
            yield return new WaitForFixedUpdate();
        }
        if (!IsGone(enemy)) { Assert.Fail("Could not destroy the Enemy; quest untested."); }

        // Return to the NPC. Interact until the key is given.
        yield return HoldPlayerAt(player, besideNpc, 5);
        interact.Invoke(npcComp, null);
        yield return HoldPlayerAt(player, besideNpc, 5);
        for (int i = 0; i < 20 && !ReadBool(keyOwner, "hasKey"); i++)
        {
            advance.Invoke(npcComp, null);
            yield return HoldPlayerAt(player, besideNpc, 2);
        }

        Assert.IsTrue(ReadBool(keyOwner, "hasKey"), "Returning to the NPC after the dialogue and the Enemy's defeat did not give the Player the key.");
    }

    [UnityTest]
    public IEnumerator DoorTest()
    {
        GameObject player = FindOrFail("Player");
        GameObject door = FindOrFail("Door");
        Component keyOwner = FindComponentWithMember(player, "hasKey");
        if (keyOwner == null) { Assert.Fail("hasKey field not found in Player components."); }

        Vector3 doorPos = door.transform.position;
        Vector3 beforeDoor = doorPos + Vector3.left * 3f;

        // Without the key, the Door is solid and touching it does not open it.
        Assert.IsFalse(DoorIsOpen(door), "Door has no solid collider, so it cannot block the Player.");
        yield return HoldPlayerAt(player, doorPos, 25);
        Assert.IsFalse(DoorIsOpen(door), "Door opened without the key.");

        // With the key, touching the Door opens it. Step away first so contact starts afresh.
        WriteBool(keyOwner, "hasKey", true);
        yield return HoldPlayerAt(player, beforeDoor, 5);
        for (int i = 0; i < 50 && !DoorIsOpen(door); i++)
        {
            Teleport(player, doorPos);
            yield return new WaitForFixedUpdate();
        }

        Assert.IsTrue(DoorIsOpen(door), "Door did not open when the Player touched it with hasKey true.");
    }

    [UnityTest]
    public IEnumerator GatedWinTest()
    {
        GameObject player = FindOrFail("Player");
        GameObject goal = FindOrFail("Goal");
        GameObject levelManagerObject = FindOrFail("LevelManager");
        Component levelManager = ComponentOrFail(levelManagerObject, "LevelManager");
        Func<bool> winCondition = GetWinCondition(levelManager);
        if (winCondition == null) { Assert.Fail("winCondition field not found in LevelManager component."); }

        GameObject enemy = FindOrFail("Enemy");
        Component enemyComp = ComponentOrFail(enemy, "Enemy");
        MethodInfo takeDamage = GetTakeDamage(enemyComp.GetType());
        if (takeDamage == null) { Assert.Fail("TakeDamage(int) method not found in Enemy component."); }

        GameObject door = FindOrFail("Door");
        Vector3 doorPos = door.transform.position;
        Component keyOwner = FindComponentWithMember(player, "hasKey");
        if (keyOwner == null) { Assert.Fail("hasKey field not found in Player components."); }

        Vector3 away = player.transform.position;
        Vector3 goalPos = goal.transform.position;

        // Reaching the Goal with the Enemy alive and the Door closed must NOT win.
        yield return HoldPlayerAt(player, goalPos, 20);
        Assert.IsFalse(winCondition(), "Win triggered while the Enemy was alive and the Door closed.");
        yield return HoldPlayerAt(player, away, 5);

        // Defeat the Enemy directly. With the Door still closed, the Goal must still NOT win.
        InvokeTakeDamage(takeDamage, enemyComp, 9999);
        for (int i = 0; i < 10 && !IsGone(enemy); i++)
        {
            yield return new WaitForFixedUpdate();
        }
        if (!IsGone(enemy)) { Assert.Fail("Could not destroy the Enemy; win untested."); }

        yield return HoldPlayerAt(player, goalPos, 20);
        Assert.IsFalse(winCondition(), "Win triggered before the Door was opened.");
        yield return HoldPlayerAt(player, away, 5);

        // Open the Door with the key.
        WriteBool(keyOwner, "hasKey", true);
        for (int i = 0; i < 50 && !DoorIsOpen(door); i++)
        {
            Teleport(player, doorPos);
            yield return new WaitForFixedUpdate();
        }
        if (!DoorIsOpen(door)) { Assert.Fail("Could not open the Door; win untested."); }
        yield return HoldPlayerAt(player, away, 5);

        // Now reaching the Goal must win.
        for (int i = 0; i < 40 && !winCondition(); i++)
        {
            Teleport(player, goalPos);
            yield return new WaitForFixedUpdate();
        }
        Assert.IsTrue(winCondition(), "Player did not win after defeating the Enemy, opening the Door and reaching the Goal.");
    }
}