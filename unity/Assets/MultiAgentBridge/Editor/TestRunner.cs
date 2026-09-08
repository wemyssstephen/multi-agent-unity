using Newtonsoft.Json.Linq;

using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace MultiAgentBridge
{
    public class TestRunner
    {
        private static string testResult = null;

        public static string RunTests(JObject args)
        {
            var testName = args["TestName"]?.ToString();
            if (string.IsNullOrEmpty(testName))
            {
                return "No test name provided.";
            }

            testResult = null;
            var testApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            testApi.RegisterCallbacks(new TestCallback());
            testApi.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.PlayMode,
                testNames = new[] { testName }
            }));

            return $"Started test run: {testName}";
        }

        public static string PollTestResult(JObject args)
        {
            return testResult ?? "running";
        }

        private class TestCallback : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void RunFinished(ITestResultAdaptor result) 
            { 
                if (testResult == null)
                {
                    testResult = "NoTestsRan";
                }
            }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.Test.IsSuite)
                {
                    testResult = result.ResultState;
                }
            }
        }
    }
}