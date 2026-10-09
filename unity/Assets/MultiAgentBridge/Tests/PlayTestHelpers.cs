using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MultiAgentBridge
{
    public class PlayTestHelpers
    {
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static GameObject FindOrFail(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go == null) { Assert.Fail($"{name} GameObject not found in the scene."); }
            return go;
        }

        // GameObject.Find skips inactive objects, so a closed UI panel would never be found.
        public static GameObject FindIncludingInactive(string name)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == name) { return t.gameObject; }
                }
            }
            return null;
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
            var field = type.GetField("winCondition", Members);
            if (field != null) { return () => (bool)field.GetValue(component); }

            var property = type.GetProperty("winCondition", Members);
            if (property != null) { return () => (bool)property.GetValue(component); }

            return null;
        }

        // Returns the first component on the GameObject with a field or property of this name.
        public static Component FindComponentWithMember(GameObject go, string name)
        {
            foreach (Component c in go.GetComponents<Component>())
            {
                if (c == null) { continue; }
                Type type = c.GetType();
                if (type.GetField(name, Members) != null || type.GetProperty(name, Members) != null) { return c; }
            }
            return null;
        }

        public static bool ReadBool(Component c, string name)
        {
            FieldInfo field = c.GetType().GetField(name, Members);
            if (field != null) { return Convert.ToBoolean(field.GetValue(c)); }
            PropertyInfo property = c.GetType().GetProperty(name, Members);
            return Convert.ToBoolean(property.GetValue(c));
        }

        public static void WriteBool(Component c, string name, bool value)
        {
            FieldInfo field = c.GetType().GetField(name, Members);
            if (field != null) { field.SetValue(c, value); return; }
            PropertyInfo property = c.GetType().GetProperty(name, Members);
            property.SetValue(c, value);
        }

        // Disables every MonoBehaviour on the Player except keepAlive.
        public static void DisablePlayerScripts(GameObject player, Component keepAlive)
        {
            foreach (MonoBehaviour mb in player.GetComponents<MonoBehaviour>())
            {
                if (mb == keepAlive) { continue; }
                mb.enabled = false;
            }
        }

        // Re-enables every MonoBehaviour on the Player.
        public static void EnablePlayerScripts(GameObject player)
        {
            foreach (MonoBehaviour mb in player.GetComponents<MonoBehaviour>())
            {
                mb.enabled = true;
            }
        }

        // Returns the Player component that owns a `health` field/property.
        public static Component FindHealthComponent(GameObject player)
        {
            foreach (Component component in player.GetComponents<Component>())
            {
                if (component == null) { continue; }
                Type type = component.GetType();
                if (type.GetField("health", Members) != null) { return component; }
                if (type.GetProperty("health", Members) != null) { return component; }
                if (GetHealthMethod(type) != null) { return component; }
            }
            return null;
        }

        public static MethodInfo GetHealthMethod(Type type)
        {
            foreach (string name in new[] { "GetHealth", "Health", "GetCurrentHealth" })
            {
                MethodInfo m = type.GetMethod(name, Members, null, Type.EmptyTypes, null);
                if (m != null && (m.ReturnType == typeof(int) || m.ReturnType == typeof(float))) { return m; }
            }
            return null;
        }

        public static Func<float> ReadHealthFrom(Component component)
        {
            Type type = component.GetType();
            FieldInfo field = type.GetField("health", Members);
            if (field != null) { return () => Convert.ToSingle(field.GetValue(component)); }

            PropertyInfo property = type.GetProperty("health", Members);
            if (property != null) { return () => Convert.ToSingle(property.GetValue(component)); }

            MethodInfo method = GetHealthMethod(type);
            if (method != null) { return () => Convert.ToSingle(method.Invoke(component, null)); }

            return null;
        }

        // Returns the first non-empty `text` shown under a UI panel.
        public static string ReadText(GameObject panel)
        {
            foreach (Component c in panel.GetComponentsInChildren<Component>())
            {
                if (c == null) { continue; }
                PropertyInfo text = c.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
                if (text != null && text.PropertyType == typeof(string))
                {
                    string value = (string)text.GetValue(c);
                    if (!string.IsNullOrEmpty(value)) { return value; }
                }
            }
            return "";
        }

        public static IEnumerator PlayerMoveHelper(GameObject player, Component mover, MethodInfo move,
                                    Vector2 direction, string label)
        {
            Vector3 start = player.transform.position;

            // First try with every Player script disabled, so input code cannot cancel Move.
            DisablePlayerScripts(player, keepAlive: null);
            for (int i = 0; i < 50; i++)
            {
                move.Invoke(mover, new object[] { direction });
                yield return new WaitForFixedUpdate();
                Vector2 moved = player.transform.position - start;
                if (Vector2.Dot(moved, direction) > 0.1f) yield break;
            }

            // If nothing moved, Move may only store input for FixedUpdate to apply, so try again with scripts enabled.
            Teleport(player, start);
            EnablePlayerScripts(player);
            for (int i = 0; i < 50; i++)
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

        // The Door is open if it is gone or no longer has an enabled, solid collider.
        public static bool DoorIsOpen(GameObject door)
        {
            if (IsGone(door)) { return true; }
            foreach (Collider2D c in door.GetComponents<Collider2D>())
            {
                if (c.enabled && !c.isTrigger) { return false; }
            }
            return true;
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

        // Keeps the Player at a position for a number of physics frames.
        public static IEnumerator HoldPlayerAt(GameObject player, Vector3 position, int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                Teleport(player, position);
                yield return new WaitForFixedUpdate();
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