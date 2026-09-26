using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Compilation;
using Unity.VisualScripting;

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
        private static readonly string WorkingScriptsRoot = Environment.GetEnvironmentVariable("BENCH_SCRIPTS_ROOT") ?? "Assets/MultiAgentBridge/Working/Scripts";

        static ToolRouter()
        {
            // Agent tools
            toolHandlers["create_gameobject"] = CreateGameObject;
            toolHandlers["create_script"] = CreateScript;
            toolHandlers["create_file"] = CreateFile;
            toolHandlers["add_component"] = AddComponent;
            toolHandlers["assign_sprite"] = AssignSprite;
            toolHandlers["list_sprites"] = ListSprites;
            toolHandlers["set_property"] = SetProperty;
            toolHandlers["create_primitive"] = CreatePrimitive;
            toolHandlers["read_scene"] = ReadScene;
            toolHandlers["read_script"] = ReadScript;
            toolHandlers["read_console"] = ReadConsole;

            // Evaluation tools
            toolHandlers["open_scene"] = OpenScene;
            toolHandlers["save_scene"] = SaveScene;
            toolHandlers["request_compile"] = RequestCompile;
            toolHandlers["check_compile"] = CheckCompile;
            toolHandlers["refresh_database"] = RefreshDatabase;
            toolHandlers["run_tests"] = TestRunner.RunTests;
            toolHandlers["poll_test_result"] = TestRunner.PollTestResult;
        }

        private class SceneNode
        {
            public string Name;
            public string GlobalObjectId;
            public List<string> Components;
            public bool ActiveSelf;
            public List<SceneNode> Children;
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

            // The model sometimes provides the filename in the TargetPath and sometimes appends .cs to ScriptName.
            // So we strip the filename off the TargetPath and strip any extensions.
            var targetDirectory = p.TargetPath;
            if (targetDirectory.EndsWith(".cs"))
            {
                targetDirectory = Path.GetDirectoryName(targetDirectory);
            }
            var scriptName = Path.GetFileNameWithoutExtension(p.ScriptName);
            var correctedFilePath = $"{targetDirectory}/{scriptName}.cs";

            if (!CheckInsideWorkingScriptsFolder(correctedFilePath))
            {
                return $"Target path must be inside {WorkingScriptsRoot}: {p.TargetPath}";
            }

            try
            {
            Directory.CreateDirectory(targetDirectory);
            File.WriteAllText(correctedFilePath, p.ScriptContent);
            }
            catch (Exception e)
            {
                return $"Failed to create script: {e.Message}";
            }
            ForceUnityUpdate(correctedFilePath);
            return $"Created {correctedFilePath}";
        }

        private static string CreateFile(JObject args)
        {
            var p = args.ToObject<CreateFileParams>();
            var fullPath = $"{WorkingScriptsRoot}/{p.FileName}";

            if (p.FileName.EndsWith(".cs"))
            {
                return "Use create_script for C# scripts.";
            }

            if (!CheckInsideWorkingScriptsFolder(fullPath))
            {
                return $"Target path must be inside {WorkingScriptsRoot}: {p.FileName}";
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllText(fullPath, p.ScriptContent);
            }
            catch (Exception e)
            {
                return $"Failed to create file: {e.Message}";
            }
            return $"Created {fullPath}";
        }

        private static string ReadScript(JObject args)
        {
            var p = args.ToObject<ReadScriptParams>();
            var assetsFolderPath = Application.dataPath.Replace('\\', '/') + "/";
            var filePath = Path.GetFullPath(p.ScriptPath).Replace('\\', '/');

            // Containment check!
            if (!filePath.StartsWith(assetsFolderPath))
            {
                return $"Target path must be inside the Assets folder: {p.ScriptPath}";
            }

            try
            {
                return File.ReadAllText(filePath);
            }
            catch (Exception e)
            {
                return $"Failed to read script: {e.Message}";
            }
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
                    return $"Failed to find component type: {p.ComponentType}. "
                            + "Unity may not have compiled correctly, check the Console.";
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

        private static string AssignSprite(JObject args)
        {
            var p = args.ToObject<AssignSpriteParams>();
            if (!TryResolveGameObject(p.GameObjectId, out var go, out var error))
            {
                return error;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(p.SpritePath);
            if (sprite == null)
            {
                return $"Failed to load sprite at path: {p.SpritePath}";
            }

            var spriteRenderer = go.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                return $"GameObject {go.name} does not have a SpriteRenderer component.";
            }

            var serialized = new SerializedObject(spriteRenderer);
            var property = serialized.FindProperty("m_Sprite");
            property.objectReferenceValue = sprite;
            serialized.ApplyModifiedProperties();

            if (!TrySaveScene()) { return "Failed to save scene"; }
            return $"Assigned sprite {p.SpritePath} to GameObject {go.name}";
        }

        private static string ListSprites(JObject args)
        {
            var folder = "Assets/MultiAgentBridge/Scenes/Sprites";
            var guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
            var paths = guids.Select(AssetDatabase.GUIDToAssetPath);
            return string.Join("\n", paths);
        }

        private static string SetProperty(JObject args)
        {
            var objectParams = args.ToObject<SetPropertyParams>();
            if (!TryResolveGameObject(objectParams.GameObjectId, out var gameObject, out var error)) { return error; }

            try
            {
                var type = FindComponentType(objectParams.ComponentType);
                if (type == null)
                {
                    return $"Failed to find component type: {objectParams.ComponentType}";
                }

                var component = gameObject.GetComponent(type);
                if (component == null)
                {
                    return $"GameObject {gameObject.name} does not have a component of type {objectParams.ComponentType}. Consider that it may have a different internal name.";
                }

                var serializedComponent = new SerializedObject(component);
                var property = serializedComponent.FindProperty(objectParams.PropertyPath);
                if (property == null)
                {
                    property = serializedComponent.FindProperty("m_" + Capitalise(objectParams.PropertyPath));
                }

                if (property == null)
                {
                    return $"Failed to find property {objectParams.PropertyPath} on component {objectParams.ComponentType}";
                }

                if (!ApplyValueToCorrectProperty(property, objectParams, out var applyError)) { return applyError; }
                serializedComponent.ApplyModifiedProperties();
                if (!TrySaveScene()) { return "Failed to save scene"; }
            }
            catch (Exception e) { return $"Failed to set property: {e.Message}";}

            return $"Set property {objectParams.PropertyPath} of component {objectParams.ComponentType} on GameObject {gameObject.name} to {objectParams.Value}";
        }

        private static string CreatePrimitive(JObject args)
        {
            var p = args.ToObject<CreatePrimitiveParams>();
            if (!Enum.TryParse<PrimitiveType>(p.PrimitiveName, ignoreCase: true, out var type))
            {
                return $"Unknown primitive type: {p.PrimitiveName}";
            }
            var go = GameObject.CreatePrimitive(type);
            go.name = p.ObjectName;
            if (!TrySaveScene()) { return "Failed to save scene"; }
            var objectId = GlobalObjectId.GetGlobalObjectIdSlow(go).ToString(); // TODO: add guard against null return value, which can happen
            return $"Created primitive {go.name}. ID: {objectId}";
        }

        private static string ReadScene(JObject args)
        {
            var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            var nodes = rootObjects.Select(BuildNode).ToList();
            return JsonConvert.SerializeObject(nodes);
        }

        private static string ReadConsole(JObject args)
        {
            return ConsoleReader.GetLogs();
        }
        
        private static void ForceUnityUpdate(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static bool TryResolveGameObject(string id, out GameObject gameObject, out string error)
        {
            gameObject = null;
            error = null;

            if (!GlobalObjectId.TryParse(id, out var globalObjectId))
            {
                error = $"Failed to parse GlobalObjectId: {id}";
                return false;
            }

            var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId);
            gameObject = obj as GameObject;
            if (gameObject == null)
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

        private static string OpenScene(JObject args)
        {
            var path = args["ScenePath"]?.ToString();
            if (string.IsNullOrEmpty(path))
            {
                return "Failed to open scene: no ScenePath provided.";
            }
            try
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                ForceUnityUpdate(path);
            }
            catch (Exception e)
            {
                return $"Failed to open scene: {e.Message}";
            }
            return $"Opened scene {path}";
        }

        private static string SaveScene(JObject args)
        {
            if (!TrySaveScene()) { return "Failed to save scene"; }
            return "Saved scene";
        }

        private static string RequestCompile(JObject args)
        {
            CompilationPipeline.RequestScriptCompilation();
            return "compiling";
        }

        private static string CheckCompile(JObject args)
        {
            if (CompilationCheck.IsCompiling) { return "compiling"; }
            if (CompilationCheck.ErrorCount > 0) { return "errors"; }
            return "ready";
        }

        private static string RefreshDatabase(JObject args)
        {
            AssetDatabase.Refresh();
            return "refreshed";
        }

        private static bool CheckInsideWorkingScriptsFolder(string path)
        {
            var root = Path.GetFullPath(WorkingScriptsRoot).Replace('\\', '/');
            if (!root.EndsWith("/")) { root += "/"; }
            var full = Path.GetFullPath(path).Replace('\\', '/');
            return full.StartsWith(root);
        }

        private static bool ApplyValueToCorrectProperty(SerializedProperty objectProperty, SetPropertyParams objectParams, out string error)
        {
            switch (objectProperty.propertyType)
            {
                case SerializedPropertyType.Float:
                    objectProperty.floatValue = objectParams.Value.Value<float>();
                    break;
                
                case SerializedPropertyType.Vector2:
                    objectProperty.vector2Value = ParseVector2(objectParams.Value);
                    break;

                case SerializedPropertyType.Vector3:
                    objectProperty.vector3Value = ParseVector3(objectParams.Value);
                    break;
                
                case SerializedPropertyType.Color:
                    objectProperty.colorValue = ParseColor(objectParams.Value);
                    break;
                
                case SerializedPropertyType.Boolean:
                    objectProperty.boolValue = objectParams.Value.Value<bool>();
                    break;
                
                case SerializedPropertyType.Integer:
                    objectProperty.intValue = objectParams.Value.Value<int>();
                    break;
                
                case SerializedPropertyType.String:
                    objectProperty.stringValue = objectParams.Value.Value<string>();
                    break;

                case SerializedPropertyType.ObjectReference:
                    var referenceID = objectParams.Value.Value<string>();
                    if (!TryResolveGameObject(referenceID, out var referenceObject, out var referenceError))
                    {
                        error = referenceError;
                        return false;
                    }

                    if (string.IsNullOrEmpty(objectParams.ReferenceComponentType))
                    {
                        objectProperty.objectReferenceValue = referenceObject;
                    }
                    else
                    {
                        var referenceType = FindComponentType(objectParams.ReferenceComponentType);
                        if (referenceType == null)
                        {
                            error = $"Failed to find component type: {objectParams.ReferenceComponentType}";
                            return false;
                        }

                        var comp = referenceObject.GetComponent(referenceType);
                        if (comp == null)
                        {
                            error = $"GameObject {referenceObject.name} does not have a component of type {objectParams.ReferenceComponentType}. Consider that it may have a different internal name.";
                            return false;
                        }
                        
                        objectProperty.objectReferenceValue = comp;
                    }
                    break;
                
                case SerializedPropertyType.Enum:
                    var enumType = FindComponentType(objectParams.ComponentType)
                        ?.GetProperty(objectParams.PropertyPath)?.PropertyType;

                    if (objectParams.Value.Type == JTokenType.Integer || enumType == null)
                    {
                        objectProperty.intValue = objectParams.Value.Value<int>();
                    }
                    else
                    {
                        objectProperty.intValue = Convert.ToInt32(
                        Enum.Parse(enumType, objectParams.Value.Value<string>(), true));
                    }
                    break;
                
                default:
                    error = $"Unsupported property type: {objectProperty.propertyType}";
                    return false;
            }
            error = null;
            return true;
        }   

        private static string Capitalise(string path)
        {
            if (string.IsNullOrEmpty(path)) { return path; }
            return char.ToUpper(path[0]) + path.Substring(1);
        }

        private static Vector3 ParseVector3(JToken value)
        {
            if (value.Type == JTokenType.Array)
            {
                var v = value.ToObject<float[]>();
                return new Vector3(v[0], v[1], v[2]);
            }
            float z = value["z"] != null ? value["z"].Value<float>() : 0f;
            return new Vector3(value["x"].Value<float>(), value["y"].Value<float>(), z);
        }

        private static Vector2 ParseVector2(JToken value)
        {
            if (value.Type == JTokenType.Array)
            {
                var v = value.ToObject<float[]>();
                return new Vector2(v[0], v[1]);
            }
            return new Vector2(
                value["x"].Value<float>(),
                value["y"].Value<float>());
        }

        private static Color ParseColor(JToken value)
        {
            if (value.Type == JTokenType.Array)
            {
                var c = value.ToObject<float[]>();
                float alpha = c.Length > 3 ? c[3] : 1f;
                return new Color(c[0], c[1], c[2], alpha);
            }
            float a = value["a"] != null ? value["a"].Value<float>() : 1f;
            return new Color(
                value["r"].Value<float>(),
                value["g"].Value<float>(),
                value["b"].Value<float>(),
                a);
        }
        
        private static SceneNode BuildNode(GameObject gameObject)
        {
            SceneNode node = new SceneNode();
            node.Name = gameObject.name;
            node.GlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(gameObject).ToString();
            node.Components = gameObject.GetComponents<Component>()
                                        .Where(c => c != null)
                                        .Select(c => c.GetType().Name)
                                        .ToList();
            node.ActiveSelf = gameObject.activeSelf;
            node.Children = new List<SceneNode>();
            foreach (Transform child in gameObject.transform)
            {
                node.Children.Add(BuildNode(child.gameObject));
            }
            return node;
        }
    }
}