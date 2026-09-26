using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MultiAgentBridge
{
    /// <summary>
    /// Runs a batch of PlayMode tests as one Test Framework run and reports a result per test.
    /// </summary>
    [InitializeOnLoad]
    public static class TestRunner
    {
        private static bool runInProgress;
        private static bool runEnded;
        private static string runError;
        private static string preRunScenePath;
        private static string[] requestedTests = new string[0];
        private static readonly Dictionary<string, string> results = new();
        private static readonly Dictionary<string, string> messages = new();

        static TestRunner()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.hideFlags = HideFlags.HideAndDontSave;
            api.RegisterCallbacks(new TestCallback());
        }

        public static string RunTests(JObject args)
        {
            string[] names = ReadTestNames(args);
            if (names.Length == 0)
            {
                return "No test names provided.";
            }

            if (runInProgress)
            {
                return "Busy: a test run is still active.";
            }

            results.Clear();
            messages.Clear();
            runError = null;
            runEnded = false;
            requestedTests = names;
            preRunScenePath = SceneManager.GetActiveScene().path;
            runInProgress = true;

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.hideFlags = HideFlags.HideAndDontSave;
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.PlayMode,
                testNames = names
            }));

            return $"Started test run ({names.Length} tests): {string.Join(", ", names)}";
        }

        public static string PollTestResult(JObject args)
        {
            if (!runInProgress)
            {
                return BuildResults("error", "No test run has been started.");
            }

            // Still running, or finished but Play Mode hasn't exited yet.
            if (!runEnded || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return "running";
            }

            // A clean run must also restore the original scene. An errored run may never
            // do that, so it only waits for Play Mode to exit.
            if (runError == null && !TeardownComplete())
            {
                return "running";
            }

            runInProgress = false;
            return BuildResults(runError == null ? "finished" : "error", runError);
        }

        private static bool TeardownComplete()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return false;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).name.StartsWith("InitTestScene"))
                {
                    return false;   // the test run temporary scene is still open
                }
            }

            // RestoreSceneSetup puts back the scene that was active before the run.
            return SceneManager.GetActiveScene().path == preRunScenePath;
        }

        private static string BuildResults(string status, string error)
        {
            var perTest = new JObject();
            foreach (string name in requestedTests)
            {
                // A requested test with no result never ran (run aborted, or name didn't match).
                perTest[name] = results.TryGetValue(name, out var state) ? state : "Missing";
            }

            var perTestMessage = new JObject();
            foreach (var entry in messages)
            {
                perTestMessage[entry.Key] = entry.Value;
            }

            var payload = new JObject
            {
                ["status"] = status,
                ["messages"] = perTestMessage,
                ["results"] = perTest
            };

            if (error != null)
            {
                payload["error"] = error;
            }
            return payload.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static string[] ReadTestNames(JObject args)
        {
            var tokens = args["TestNames"];
            if (tokens == null) return Array.Empty<string>();
            return tokens.Select(t => t.ToString()).Where(s => !string.IsNullOrEmpty(s)).ToArray();
        }

        private class TestCallback : ICallbacks, IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.Test.IsSuite)
                {
                    results[result.Test.FullName] = result.ResultState;
                    if (result.TestStatus == TestStatus.Failed)
                    {
                        messages[result.Test.FullName] = result.Message ?? "";
                    }
                }
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                runEnded = true;
            }

            public void OnError(string message)
            {
                // The framework's "An unexpected error happened while running tests" path.
                // Without this, an errored run never reports and the poll sits on "running".
                runError = message;
                runEnded = true;
            }
        }
    }
}