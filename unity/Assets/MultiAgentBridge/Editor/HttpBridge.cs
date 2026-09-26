using System;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEditor;
using System.Net.Sockets;

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
        private const int PortRetries = 20;
        private const int PortRetryDelay = 200;
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
            if (!TryStartListener())
            {
                Debug.LogError($"[HttpBridge] Could not bind :{Port} after {PortRetries} attempts — bridge is DOWN for this domain.");
                return;
            }

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
            catch (ObjectDisposedException)
            {
                Debug.Log("[HttpBridge] Listener closed for domain reload.");
            }
        }

        private static bool TryStartListener()
        {
            listener.Prefixes.Add($"http://localhost:{Port}/");
            for (int attempt = 1; attempt <= PortRetries; attempt++)
            {
                try
                {
                    listener.Start();
                    Debug.Log($"[HttpBridge] Listening on http://localhost:{Port}/ (bound on attempt {attempt}).");
                    return true;
                }
                catch (SocketException ex)
                {
                    Debug.Log($"[HttpBridge] :{Port} busy, attempt {attempt}/{PortRetries} ({ex.SocketErrorCode}); previous domain's socket still releasing, retrying in {PortRetryDelay}ms.");
                    Thread.Sleep(PortRetryDelay);
                }
                catch (HttpListenerException ex)
                {
                    Debug.Log($"[HttpBridge] Start failed, attempt {attempt}/{PortRetries} ({ex.Message}); retrying in {PortRetryDelay}ms.");
                    Thread.Sleep(PortRetryDelay);
                }
            }
            return false;
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
