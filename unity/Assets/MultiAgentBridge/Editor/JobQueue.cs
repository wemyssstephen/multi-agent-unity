using System;
using System.Collections.Concurrent;
using UnityEngine;
using UnityEditor;

namespace MultiAgentBridge
{   /// <summary>
    /// Runs jobs on Unity's main thread. Jobs are enqeueued here. 
    /// <see cref="Tick"/> runs on the Editor update loop (main thread) 
    /// and pops jobs from the queue and runs them. Results are stored in a concurrent dictionary and polled by the caller. 
    /// </summary>
    [InitializeOnLoad]
    public static class JobQueue
    {
        private static readonly ConcurrentQueue<Action> jobsQueue = new();
        private static readonly ConcurrentDictionary<string, string> jobsResults = new();

        static JobQueue()
        {
            Debug.Log("JobQueue ctor ran.");
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        /// <summary>
        /// Queues a job to run on the main thread. The job's return value is stored
        /// under <paramref name="jobId"/>, retrievable via <see cref="PollJobResult"/>.
        /// </summary>
        /// <param name="jobId">Unique id used to retrieve the result later.</param>
        /// <param name="job">Work to run on the main thread; its result is stored.</param>
        public static void EnqueueJob(string jobId, Func<string> job)
        {
            jobsQueue.Enqueue(() =>
            {
                var result = job();
                jobsResults.TryAdd(jobId, result);
            });
        }
        /// <summary>
        /// Returns the result for <paramref name="jobId"/> if the job has finished,
        /// otherwise null. The result is removed on read, so it returns non-null once.
        /// </summary>
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
            // TODO: job() has no try/catch — one throwing job kills the pump (breaks out of Tick).
            // Wrap so a failed job records an error result instead. Errors should carry context for the caller.
            while (jobsQueue.TryDequeue(out var job))
            {
                job();
            }
        }
    }
}