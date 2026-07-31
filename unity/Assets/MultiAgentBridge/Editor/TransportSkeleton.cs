using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;
using UnityEditor;

namespace MultiAgentBridge
{   
    [InitializeOnLoad]
    public static class TransportSkeleton
    {

        private static int tickCount;
        private static readonly ConcurrentQueue<Action> jobs = new();

        static TransportSkeleton()
        {
            Debug.Log("TransportSkeleton ctor ran.");
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;

            // Create the background thread and enqueue any jobs received.
            var t = new Thread(() =>
            {
               jobs.Enqueue(() =>
               {
                   new GameObject("BridgeProof");
               });
            });
            t.Start();
        }

        private static void Tick()
        {
            tickCount += 1;
            if (tickCount % 1000 == 0)
            {
                Debug.Log($"Tick Count: {tickCount}");
            }

            // Called on every update. It pops a job from the queue and evokes it. If empty, it returns false and the loop stops.
            while (jobs.TryDequeue(out var job))
            {
                job();
            }
        }
    }
}