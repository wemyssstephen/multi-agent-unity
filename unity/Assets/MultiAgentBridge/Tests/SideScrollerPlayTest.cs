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
        if (player == null) { Assert.Fail("Player GameObject not found in the scene."); yield break;}

        Component component = player.GetComponent("PlayerMover");
        if (component == null) { Assert.Fail("PlayerMover component not found on Player GameObject."); yield break;}

        MethodInfo move = component.GetType().GetMethod("Move", new[] { typeof(Vector2) });
        if (move == null) { Assert.Fail("Move(Vector2) method not found in PlayerMover component"); yield break;}
        
        Vector3 start = player.transform.position;
        move.Invoke(component, new object[] { Vector2.right });
        yield return null; // Wait for one frame to allow the movement to take effect
        Vector3 end  = player.transform.position;

        Assert.Greater(end.x, start.x, "Player did not move to the right as expected.");
        Assert.AreEqual(start.y, end.y, 0.01f, "Player y drifted.");
    }
}