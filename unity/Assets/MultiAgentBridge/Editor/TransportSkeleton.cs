using System;
using System.Collections.Concurrent;
using UnityEngine;
using UnityEditor;

namespace MultiAgentBridge
{   
    [InitializeOnLoad]
    public static class TransportSkeleton
    {

        private static int tickCount;
        private static readonly ConcurrentQueue<Action> jobsQueue = new();
        private static readonly ConcurrentDictionary<string, string> jobsResults = new();

        static TransportSkeleton()
        {
            Debug.Log("TransportSkeleton ctor ran.");
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        public static void EnqueueJob(string jobId, Func<string> job)
        {
            jobsQueue.Enqueue(() =>
            {
                var result = job();
                jobsResults.TryAdd(jobId, result);
            });
        }

        public static string PollJobResult(string jobId)
        {
            if (jobsResults.TryRemove(jobId, out var result))
            {
                return result;
            }
            return null;
        }

        private static void Tick()
        {
            tickCount += 1;
            if (tickCount % 1000 == 0)
            {
                Debug.Log($"Tick Count: {tickCount}");
            }

            // Called on every update. It pops a job from the queue and evokes it. If empty, it returns false and the loop stops.
            while (jobsQueue.TryDequeue(out var job))
            {
                job();
            }
        }
    }
}