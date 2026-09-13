using System;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEditor;

namespace MultiAgentBridge
{
    /// <summary>
    /// HTTP entry point for the bridge. Listens on localhost:8080 on a background
    /// thread, reads each request body, and hands it to <see cref="JobQueue"/> to run
    /// on the main thread. Started automatically on load and after every domain reload via InitializeOnLoad.
    /// </summary>
    [InitializeOnLoad]
    public static class HttpBridge
    {
        
        private const int Port = 8080;
        private const int HandlerTimeOut = 120000;
        private const int PollSleep = 50;
        private static readonly HttpListener listener = new();

        static HttpBridge()
        {
            Debug.Log("HttpBridge ctor ran.");

            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

            // HttpListener running on a background thread
            var t = new Thread(RunListener) { IsBackground = true };
            t.Start();
        }

        private static void RunListener()
        {
            listener.Prefixes.Add($"http://localhost:{Port}/");
            listener.Start();
            Debug.Log($"Listening for HTTP requests on http://localhost:{Port}/");

            try
            {
                while (true)
                {
                    var context = listener.GetContext();
                    var response = context.Response;
                    var output = response.OutputStream;

                    // Read the request body
                    string requestBody;
                    using (var reader = new System.IO.StreamReader(context.Request.InputStream))
                    {
                        requestBody = reader.ReadToEnd();
                    }

                    // Handle the request and wait for a response
                    try
                    {
                        string jobId = MintId();
                        string pollResult = null;

                        JobQueue.EnqueueJob(jobId, () => ToolRouter.RunTool(requestBody));

                        while (pollResult == null)
                        {
                            pollResult = JobQueue.PollJobResult(jobId);
                            if (pollResult == null) { Thread.Sleep(PollSleep); }
                        }

                        if (pollResult == null)
                        {
                            Thread.Sleep(50);
                        }

                        byte[] buffer = Encoding.UTF8.GetBytes(pollResult);
                        response.ContentLength64 = buffer.Length;
                        output.Write(buffer, 0, buffer.Length);
                    }

                    finally
                    {
                        output.Close();
                    }
                }
            }
            catch (HttpListenerException ex)
            {
                Debug.Log($"HttpListenerException: {ex.Message}");
            }
        }

        private static string MintId()
        {
            return Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Closes the listener before a domain reload so the port is released.
        /// </summary>
        private static void OnBeforeAssemblyReload()
        {
            listener.Close();
        }
    }
}
