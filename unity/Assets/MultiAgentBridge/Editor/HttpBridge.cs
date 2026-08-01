using System;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEditor;

namespace MultiAgentBridge
{
    [InitializeOnLoad]

    public static class HttpBridge
    {
        static HttpBridge()
        {
            Debug.Log("HttpBridge ctor ran.");
            // HttpListener running on a background thread
            var t = new Thread(() =>
            {
                RunListener();
            });            
            t.Start();
        }

        private static void RunListener()
        {
            var listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:8080/");
            listener.Start();
            Debug.Log("Listening for HTTP requests on http://localhost:8080/");

            while (true)
            {
                var context = listener.GetContext();
                var response = context.Response;
                var output = response.OutputStream;

                // Handle the request and wait for a response
                try
                {
                    string jobId = MintId();
                    string pollResult = null;

                    TransportSkeleton.EnqueueJob(jobId, () =>
                    {
                        new GameObject("BridgeProof");
                        return "created BridgeProof";
                    });

                    while (pollResult == null)
                    {
                        pollResult = TransportSkeleton.PollJobResult(jobId);
                        if (pollResult == null) Thread.Sleep(50); // Sleep for a short duration to avoid busy waiting
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

        private static string MintId()
        {
            return Guid.NewGuid().ToString();
        }
    }
}
