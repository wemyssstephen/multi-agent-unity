using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace MultiAgentBridge
{
    public class PlayTestHelpers
    {
        public static GameObject FindOrFail(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go == null) { Assert.Fail($"{name} GameObject not found in the scene."); }
            return go;
        }

        public static Component ComponentOrFail(GameObject go, string type)
        {
            Component c = go.GetComponent(type);
            if (c == null) { Assert.Fail($"{type} component not found on {go.name} GameObject."); }
            return c;
        }

        public static MethodInfo MethodOrFail(Component c, string name, params Type[] args)
        {
            MethodInfo m = c.GetType().GetMethod(name, args);
            if (m == null) { Assert.Fail($"{name} method not found in {c.GetType().Name} component."); }
            return m;
        }

        public static Func<bool> GetWinCondition(Component component)
        {
            var type = component.GetType();
            var field = type.GetField("winCondition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) { return () => (bool)field.GetValue(component); }

            var property = type.GetProperty("winCondition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null) { return () => (bool)property.GetValue(component); }

            return null;
        }

        // Disables every MonoBehaviour on the Player except keepAlive (pass null to disable all).
        public static void DisablePlayerScripts(GameObject player, Component keepAlive)
        {
            foreach (MonoBehaviour mb in player.GetComponents<MonoBehaviour>())
            {
                if (mb == keepAlive) { continue; }
                mb.enabled = false;
            }
        }

        // Returns the Player component that owns a `health` field/property, so the caller can keep it
        // enabled while disabling the rest.
        public static Component FindHealthComponent(GameObject player)
        {
            foreach (Component component in player.GetComponents<Component>())
            {
                Type type = component.GetType();
                if (type.GetField("health", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null) { return component; }
                if (type.GetProperty("health", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null) { return component; }
                if (GetHealthMethod(type) != null) { return component; }
            }
            return null;
        }

        public static MethodInfo GetHealthMethod(Type type)
        {
            foreach (string name in new[] { "GetHealth", "Health", "GetCurrentHealth" })
            {
                MethodInfo m = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (m != null && (m.ReturnType == typeof(int) || m.ReturnType == typeof(float))) { return m; }
            }
            return null;
        }

        public static Func<float> ReadHealthFrom(Component component)
        {
            Type type = component.GetType();
            FieldInfo field = type.GetField("health", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) { return () => Convert.ToSingle(field.GetValue(component)); }

            PropertyInfo property = type.GetProperty("health", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null) { return () => Convert.ToSingle(property.GetValue(component)); }

            MethodInfo method = GetHealthMethod(type);
            if (method != null) { return () => Convert.ToSingle(method.Invoke(component, null)); }

            return null;
        }

        public static IEnumerator PlayerMoveHelper(GameObject player, Component mover, MethodInfo move,
                                    Vector2 direction, string label)
        {
            // Trying to isolate Move by disabling the MonoBehaviour of the player.
            // Some agent implementations zero velocity when no key is pressed meaning we cannot invoke via reflection and read cleanly.
            foreach (var mb in player.GetComponents<MonoBehaviour>())
                mb.enabled = false;
            
            Vector3 start = player.transform.position;

            for (int i = 0; i< 50; i++)
            {
                move.Invoke(mover, new object[] { direction });
                yield return new WaitForFixedUpdate();
                Vector2 moved = player.transform.position - start;
                if (Vector2.Dot(moved, direction) > 0.1f) yield break;
            }

            Vector2 finalMoved = player.transform.position - start;
            Assert.Greater(Vector2.Dot(finalMoved, direction), 0.1f,
            $"Move ({label}) did not move the Player in the {label} direction.");
        }
    
        // Destroyed or deactivated.
        public static bool IsGone(GameObject go)
        {
            return go == null || !go.activeInHierarchy;
        }
    
        // Moves the Player instantly and stops it.
        public static void Teleport(GameObject player, Vector3 position)
        {
            player.transform.position = position;
            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.position = position;
                rb.linearVelocity = Vector2.zero;
            }
        }
    
        // TakeDamage(int) as specified; TakeDamage(float) accepted too.
        public static MethodInfo GetTakeDamage(Type type)
        {
            MethodInfo method = type.GetMethod("TakeDamage", new[] { typeof(int) });
            if (method == null) { method = type.GetMethod("TakeDamage", new[] { typeof(float) }); }
            return method;
        }
    
        public static void InvokeTakeDamage(MethodInfo takeDamage, Component enemy, int amount)
        {
            object argument = amount;
            if (takeDamage.GetParameters()[0].ParameterType == typeof(float)) { argument = (float)amount; }
            takeDamage.Invoke(enemy, new[] { argument });
        }
    
        // True if HealthBar, or a UI parent of it below the Canvas, uses the top-left anchor preset.
        public static bool AnchoredTopLeft(GameObject bar)
        {
            Vector2 topLeft = new Vector2(0f, 1f);
            Transform t = bar.transform;
            while (t != null && t.GetComponent<Canvas>() == null)
            {
                RectTransform rt = t as RectTransform;
                if (rt != null
                    && Vector2.Distance(rt.anchorMin, topLeft) < 0.01f
                    && Vector2.Distance(rt.anchorMax, topLeft) < 0.01f)
                {
                    return true;
                }
                t = t.parent;
            }
            return false;
        }
    }
}