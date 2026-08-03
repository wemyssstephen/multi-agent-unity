
using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

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
            return $"created {p.ObjectName}";
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

        private static void ForceUnityUpdate(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            EditorApplication.QueuePlayerLoopUpdate();
        }
    }
}