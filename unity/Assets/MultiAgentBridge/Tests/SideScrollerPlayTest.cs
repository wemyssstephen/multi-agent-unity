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
    }

    [UnityTest]
    public IEnumerator PlayerGravityTest()
    {
        GameObject player = FindOrFail("Player");
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb == null) { Assert.Fail("Side-scroller Player has no Rigidbody2D, so it cannot fall under gravity."); }

        Assert.Greater(rb.gravityScale, 0f, "Side-scroller Player has a Rigidbody2D but gravity is disabled.");
        yield return null;
    }

    [UnityTest]
    public IEnumerator JumpTest()
    {
        GameObject player = FindOrFail("Player");
        Component mover = ComponentOrFail(player, "PlayerMover");
        MethodInfo jump = MethodOrFail(mover, "Jump", typeof(Vector2));
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb == null) { Assert.Fail("Player has no Rigidbody2D."); }

        // let the Player land and come to rest.
        for (int i = 0; i < 100; i++)
        {
            yield return new WaitForFixedUpdate();
        }
        Assert.Less(Mathf.Abs(rb.linearVelocity.y), 0.01f, "Player never came to rest on the ground.");

        // jump and track the highest point reached.
        float startY = player.transform.position.y;
        float highestY = startY;
        jump.Invoke(mover, new object[] { Vector2.up });
        for (int i = 0; i < 300; i++)
        {
            yield return new WaitForFixedUpdate();
            highestY = Mathf.Max(highestY, player.transform.position.y);
        }
        float endY = player.transform.position.y;

        // check it went up, then came back down.
        Assert.Greater(highestY, startY + 0.5f, "Player did not jump.");
        Assert.Less(endY, highestY - 0.3f, "Player jumped but never came back down.");
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

        // If nothing moved, try again with scripts enabled.
        if (player.transform.position.x >= playerStart.x)
        {
            Teleport(player, playerStart);
            EnablePlayerScripts(player);
            for (int i = 0; i < 100; i++)
            {
                move.Invoke(mover, new object[] { Vector2.left });
                yield return new WaitForFixedUpdate();
            }
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
        float lowest = start.y;
        for (int i = 0; i < 150; i++)
        {
            yield return new WaitForFixedUpdate();
            if (IsGone(enemy)) { break; }
            Vector2 now = enemy.transform.position;
            furthest = Mathf.Max(furthest, Mathf.Abs(now.x - start.x));
            lowest = Mathf.Min(lowest, now.y);
        }

        Assert.Less(start.y - lowest, 1f, "Enemy fell out of the level instead of patrolling.");
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

        Assert.AreEqual(readHealth(), readBar(), 0.01f, "HealthBar does not show the Player's starting health.");

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

        yield return HoldPlayerAt(player, away, 5);
        for (int i = 0; i < 5; i++)
        {
            yield return null;
        }

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

        // Stand beside the NPC and talk to it.
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

        // Defeat the Enemy
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

        // Check the Door collider actually works
        Component mover = ComponentOrFail(player, "PlayerMover");
        MethodInfo move = MethodOrFail(mover, "Move", typeof(Vector2));
        yield return HoldPlayerAt(player, beforeDoor, 5);
        DisablePlayerScripts(player, keepAlive: null);
        for (int i = 0; i < 100; i++)
        {
            move.Invoke(mover, new object[] { Vector2.right });
            yield return new WaitForFixedUpdate();
        }

        // If nothing moved, Move may only store input for FixedUpdate to apply, so try again with scripts enabled.
        if (player.transform.position.x <= beforeDoor.x)
        {
            Teleport(player, beforeDoor);
            EnablePlayerScripts(player);
            for (int i = 0; i < 100; i++)
            {
                move.Invoke(mover, new object[] { Vector2.right });
                yield return new WaitForFixedUpdate();
            }
        }

        Assert.Less(player.transform.position.x, doorPos.x, "Player passed through the closed Door.");
        move.Invoke(mover, new object[] { Vector2.zero });
        EnablePlayerScripts(player);

        // Check the door opens with a key
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

        // Reaching the Goal with the Enemy alive and the Door closed does not win.
        yield return HoldPlayerAt(player, goalPos, 20);
        Assert.IsFalse(winCondition(), "Win triggered while the Enemy was alive and the Door closed.");
        yield return HoldPlayerAt(player, away, 5);

        // Enemy dead but door closed does not win.
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