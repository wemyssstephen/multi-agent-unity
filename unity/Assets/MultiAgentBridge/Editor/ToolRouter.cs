using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace MultiAgentBridge
{
    /// <summary>
    /// Maps tool names to handlers and runs the one a request asks for.
    /// Handlers touch the Unity API, so RunTool must be called on the main thread
    /// (via <see cref="JobQueue"/>), never from the listener thread directly.
    /// </summary>
    public static class ToolRouter
    {
        private static readonly Dictionary<string, Func<JObject, string>> toolHandlers = new();
        static ToolRouter()
        {
            toolHandlers["create_gameobject"] = CreateGameObject;
            toolHandlers["create_script"] = CreateScript;
            toolHandlers["add_component"] = AddComponent;
        }

        /// <summary>
        /// Parses a tool request, finds the handler by name, and runs it.
        /// Returns the handler's result, or an error string if the JSON is invalid
        /// or the tool name is unknown — errors come back as results so the caller
        /// can read and recover. 
        /// </summary>
        public static string RunTool(string requestJson)
        {
            ToolRequest request;
            try
            {
                request = JsonConvert.DeserializeObject<ToolRequest>(requestJson);
            }
            catch (JsonException err)
            {
                return $"Failed to deserialize request: {err.Message}";
            }

            if (!toolHandlers.TryGetValue(request.Name, out var handler))
            {
                return $"Unknown tool name: {request.Name}";
            }
            
            return handler(request.Args);
        }

        private static string CreateGameObject(JObject args)
        {
            var p = args.ToObject<CreateGameObjectParams>();
            var go = new GameObject(p.ObjectName);
            if (!TrySaveScene()) { return "Failed to save scene"; }
            var objectId = GlobalObjectId.GetGlobalObjectIdSlow(go).ToString(); // TODO: add guard against null return value, which can happen
            return $"Created {go.name}. ID: {objectId}";
        }

        private static string CreateScript(JObject args)
        {
            var p = args.ToObject<CreateScriptParams>();
            var path = $"{p.TargetPath}/{p.ScriptName}.cs";
            var assetsFolderPath = Application.dataPath.Replace('\\', '/') + "/";
            var filePath = Path.GetFullPath(path).Replace('\\', '/');

            if (!filePath.StartsWith(assetsFolderPath))
            {
                return $"Target path must be inside the Assets folder: {p.TargetPath}";
            }

            try
            {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, p.ScriptContent);
            }
            catch (Exception e)
            {
                return $"Failed to create script: {e.Message}";
            }
            ForceUnityUpdate(path);
            return $"Created {path}";
        }

        private static string AddComponent(JObject args)
        {
            var p = args.ToObject<AddComponentParams>();
            if (!TryResolveGameObject(p.GameObjectId, out var go, out var error))
            {
                return error;
            }

            try
            {
                var type = FindComponentType(p.ComponentType);
                if (type == null)
                {
                    return $"Failed to find component type: {p.ComponentType}";
                }
                go.AddComponent(type);
                if (!TrySaveScene()) { return "Failed to save scene"; }
            }
            catch (Exception e)
            {
                return $"Failed to add component: {e.Message}";
            }

            return $"Added component {p.ComponentType} to GameObject {go.name}";
        }

        private static void ForceUnityUpdate(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            // EditorApplication.QueuePlayerLoopUpdate();
        }

        private static bool TryResolveGameObject(string id, out GameObject go, out string error)
        {
            go = null;
            error = null;

            if (!GlobalObjectId.TryParse(id, out var globalObjectId))
            {
                error = $"Failed to parse GlobalObjectId: {id}";
                return false;
            }

            var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId);
            go = obj as GameObject;
            if (go == null)
            {
                error = $"Failed to get GameObject by ID: {id}";
                return false;
            }
        
            return true;
        }

        private static Type FindComponentType(string name)
        {
            foreach (var t in TypeCache.GetTypesDerivedFrom<Component>())
            {
                if (t.Name == name) return t;
            }
            return null;
        }

        private static bool TrySaveScene()
        {
            bool success = EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            return success;
        }
    }
}