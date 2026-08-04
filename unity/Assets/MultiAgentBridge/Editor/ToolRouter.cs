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
            toolHandlers["set_property"] = SetProperty;
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

        private static string SetProperty(JObject args)
        {
            var p = args.ToObject<SetPropertyParams>();
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

                var component = go.GetComponent(type);
                if (component == null)
                {
                    return $"GameObject {go.name} does not have a component of type {p.ComponentType}. Consider that it may have a different internal name.";
                }

                var so = new SerializedObject(component);
                var property = so.FindProperty(p.PropertyPath);
                if (property == null)
                {
                    return $"Failed to find property {p.PropertyPath} on component {p.ComponentType}";
                }

                if (!ApplyValueToCorrectProperty(property, p, out var applyError)) { return applyError; }
                so.ApplyModifiedProperties();
                if (!TrySaveScene()) { return "Failed to save scene"; }
            }
            catch (Exception e)
            {
                return $"Failed to set property: {e.Message}";
            }

            return $"Set property {p.PropertyPath} of component {p.ComponentType} on GameObject {go.name} to {p.Value}";
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
                if (t.Name == name) { return t; }
            }
            return null;
        }

        private static bool TrySaveScene()
        {
            bool success = EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            return success;
        }

        private static bool ApplyValueToCorrectProperty(SerializedProperty property, SetPropertyParams p, out string error)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    property.floatValue = p.Value.Value<float>();
                    break;

                case SerializedPropertyType.Vector3:
                    var v = p.Value.ToObject<float[]>();
                    property.vector3Value = new Vector3(v[0], v[1], v[2]);
                    break;
                
                case SerializedPropertyType.Color:
                    var c = p.Value.ToObject<float[]>();
                    property.colorValue = new Color(c[0], c[1], c[2], c[3]);
                    break;
                
                case SerializedPropertyType.Boolean:
                    property.boolValue = p.Value.Value<bool>();
                    break;
                
                case SerializedPropertyType.Integer:
                    property.intValue = p.Value.Value<int>();
                    break;
                
                case SerializedPropertyType.String:
                    property.stringValue = p.Value.Value<string>();
                    break;

                case SerializedPropertyType.ObjectReference:
                    var refId = p.Value.Value<string>();
                    if (!TryResolveGameObject(refId, out var refGo, out var refError))
                    {
                        error = refError;
                        return false;
                    }

                    if (string.IsNullOrEmpty(p.ReferenceComponentType))
                    {
                        property.objectReferenceValue = refGo;
                    }
                    else
                    {
                        var refType = FindComponentType(p.ReferenceComponentType);
                        if (refType == null)
                        {
                            error = $"Failed to find component type: {p.ReferenceComponentType}";
                            return false;
                        }

                        var comp = refGo.GetComponent(refType);
                        if (comp == null)
                        {
                            error = $"GameObject {refGo.name} does not have a component of type {p.ReferenceComponentType}. Consider that it may have a different internal name.";
                            return false;
                        }
                        
                        property.objectReferenceValue = comp;
                    }
                    break;
                
                default:
                    error = $"Unsupported property type: {property.propertyType}";
                    return false;
            }
            error = null;
            return true;
        }   
    }
}