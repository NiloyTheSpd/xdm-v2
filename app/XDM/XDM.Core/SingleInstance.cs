using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using TraceLog;
using XDM.Core.BrowserMonitoring;

namespace XDM.Core
{
    public static class SingleInstance
    {
        public static Mutex GlobalMutex;
        public static void Ensure()
        {
            // Never block here: new Mutex(initiallyOwned: false) opens or creates
            // without waiting, unlike new Mutex(true, name) which waits on a live
            // owner and would hang startup invisibly.
            GlobalMutex = new Mutex(false, @"Global\XDM_Active_Instance", out bool createdNew);
            if (createdNew)
            {
                GlobalMutex.WaitOne();
                return;
            }
            // A mutex object already exists. It may belong to a live peer or be
            // stale: the mutex file survives crashes (on Unix it lives under
            // /tmp/.dotnet/shm until reboot), so its mere existence does NOT prove
            // a live peer. Probe the IPC port first.
            if (IsPeerAlive())
            {
                SendArgsToRunningInstance();
                Environment.Exit(0);
            }
            // Stale mutex with no live peer: acquire without blocking. If a live
            // owner holds it but isn't answering HTTP (still starting up), forward
            // args best-effort and exit rather than hanging at startup.
            try
            {
                if (!GlobalMutex.WaitOne(0))
                {
                    SendArgsToRunningInstance();
                    Environment.Exit(0);
                }
            }
            catch (AbandonedMutexException)
            {
                // Previous owner died while holding it: ownership is ours now.
            }
        }

        private static bool IsPeerAlive()
        {
            try
            {
                var request = WebRequest.Create("http://127.0.0.1:8597/sync");
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Timeout = 2000;
                var data = Encoding.UTF8.GetBytes("[]");
                request.ContentLength = data.Length;
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }
                using var response = request.GetResponse();
                return true;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "No live peer on IPC port");
                return false;
            }
        }

        private static void SendArgsToRunningInstance()
        {
            try
            {
                Log.Debug("Sending to running instance...");
                var args = Environment.GetCommandLineArgs().Skip(1);
                var request = WebRequest.Create("http://127.0.0.1:8597/args");
                var postData = JsonConvert.SerializeObject(args.Count() == 0 ? new string[] { "--restore-window" } : args);
                Log.Debug("Sending...");
                var data = Encoding.UTF8.GetBytes(postData);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.ContentLength = data.Length;
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }
                var response = request.GetResponse();
                Log.Debug("Sent...");
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed sending args to running instance");
            }
        }
    }

    public class InstanceAlreadyRunningException : Exception
    {
        public InstanceAlreadyRunningException(string message) : base(message)
        {
        }
    }
}
