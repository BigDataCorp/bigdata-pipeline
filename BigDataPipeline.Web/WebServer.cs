﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Owin;
using Nancy;
using Microsoft.Owin.Extensions;
using Microsoft.Owin.Hosting;

namespace BigDataPipeline.Web
{
    public class WebServer
    {
        // http://www.jhovgaard.com/from-aspnet-mvc-to-nancy-part-1/
        // https://github.com/NancyFx/DinnerParty/blob/master/src/
        // https://github.com/NancyFx/Nancy/tree/master/src/Nancy.Demo.Hosting.Aspnet

        static IDisposable host = null;
        static string address = null;
        static int port = 0;

        static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger ();

        public static string Address
        {
            get { return address; }
        }

        // OWIN startup
        public class Startup
        {
            public void Configuration (Owin.IAppBuilder app)
            {
                // adjust owin queue and concurrent requests limits
                object listener;
                if (app.Properties.TryGetValue ("Microsoft.Owin.Host.HttpListener.OwinHttpListener", out listener))
                {
                    var l = listener as Microsoft.Owin.Host.HttpListener.OwinHttpListener;
                    if (l != null)
                    {                        
                        l.SetRequestQueueLimit (1000);
                        l.SetRequestProcessingLimits (50000, 50000);
                    }
                }
                // reduce idle connection timeout, to increase the number of concurrent clients
                if (app.Properties.TryGetValue ("System.Net.HttpListener", out listener))
                {
                    var l = listener as System.Net.HttpListener;
                    if (l != null)
                    {
                        //l.TimeoutManager.IdleConnection = TimeSpan.FromSeconds (5);
                    }
                }

                app.UseStageMarker (PipelineStage.MapHandler);
                
                // configure owin startup
                app.UseNancy (new Nancy.Owin.NancyOptions
                {
                    Bootstrapper = new ApplicationBootstrapper ()
                });                
            }
        }

        public static Type[] GetWebModulesTypes ()
        {
            return new Type[] { typeof (NancyModule) };   
        }

        public static void Start (int portNumber = 80, string siteRootPath = null, string virtualDirectoryPath = "/pipeline", bool openFirewallExceptions = false)
        {
            _logger.Debug ("[start] Starting web server endpoint...");
            // lets try to start server
            // in case of beign unable to bind to the address, lets wait and try again
            int maxTryCount = 8;
            int retry = 0;
            while (retry++ < maxTryCount && !TryToStart (portNumber, siteRootPath, virtualDirectoryPath, openFirewallExceptions))
            {
                System.Threading.Thread.Sleep (1000 << retry);
                NLog.LogManager.GetCurrentClassLogger ().Warn ("WebServer initialization try count {0}/{1}", retry, maxTryCount);
            }
            _logger.Debug ("[done] Starting web server endpoint...");
            _logger.Info ("WebServer listening to " + BigDataPipeline.Web.WebServer.Address);
        }

        public static bool TryToStart (int portNumber = 80, string siteRootPath = null, string virtualDirectoryPath = "/pipeline", bool openFirewallExceptions = false)
        {
            string url = "";
            try
            {
                if (host != null)
                    return true;
            
                // site files root path
                if (!String.IsNullOrEmpty (siteRootPath))
                    ApplicationBootstrapper.PathProvider.SetRootPath (siteRootPath);

                // adjust virtual path
                virtualDirectoryPath = (virtualDirectoryPath ?? "").Replace ('\\', '/').Replace ("//", "/").Trim ().Trim ('/');

                // adjust addresses
                port = portNumber;
                url = "http://+:" + portNumber + "/" + virtualDirectoryPath;
                address = url.Replace ("+", "localhost");

                host = WebApp.Start<Startup> (new StartOptions (url) { ServerFactory = "Microsoft.Owin.Host.HttpListener" });

                if (openFirewallExceptions)
                    Task.Factory.StartNew (OpenFirewallPort);
            }
            catch (Exception ex)
            {                
                NLog.LogManager.GetCurrentClassLogger ().Error (ex);
                if (ex.InnerException != null && ex.InnerException.Message == "Access is denied")
                { 
                    NLog.LogManager.GetCurrentClassLogger().Warn("Denied access to listen to address " + url + " . Use netsh to add user access permission. Example: netsh http add urlacl url=http://+:80/pipeline/ user=Everyone");
                }
                Stop ();
            }
            return host != null;
        }

        private static void OpenFirewallPort ()
        {
            try
            {
                System.Diagnostics.Process.Start ("netsh", "advfirewall firewall add rule name=\"BigDataPipeline port\" dir=in action=allow protocol=TCP localport=" + port).WaitForExit ();
                System.Diagnostics.Process.Start ("netsh", "advfirewall firewall add rule name=\"BigDataPipeline port\" dir=out action=allow protocol=TCP localport=" + port).WaitForExit ();
            }
            catch
            {
            }
        }

        public static void AddUrlReservationOnWindows(string url)
        {
            try
            {
                System.Diagnostics.Process.Start("netsh", "http add urlacl url=" + url + " user=Everyone").WaitForExit();
            }
            catch
            {
            }
        }

        private static Uri[] GetUriParams (List<string> dnsHosts, int port)
        {
            var uriParams = new List<Uri> ();
            string hostName = System.Net.Dns.GetHostName ();

            // Host name URI
            if (dnsHosts == null)
            {
                dnsHosts = new List<string> ();
            }

            if (!dnsHosts.Contains (hostName, StringComparer.OrdinalIgnoreCase))
            {
                dnsHosts.Add (hostName);
            }

            foreach (var name in dnsHosts)
            {
                if (String.IsNullOrWhiteSpace (name))
                    continue;
                uriParams.Add (new Uri (String.Format ("http://{0}:{1}/", name, port)));
                var hostEntry = System.Net.Dns.GetHostEntry (name);
                if (hostEntry != null)
                {
                    foreach (var ipAddress in hostEntry.AddressList)
                    {
                        if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)  // IPv4 addresses only
                        {
                            var addrBytes = ipAddress.GetAddressBytes ();
                            string hostAddressUri = String.Format ("http://{0}.{1}.{2}.{3}:{4}/",
                                addrBytes[0], addrBytes[1], addrBytes[2], addrBytes[3], port);
                            uriParams.Add (new Uri (hostAddressUri));
                        }
                    }
                }
            }

            // also add Localhost URI
            uriParams.Add (new Uri (String.Format ("http://localhost:{0}/", port)));
            return uriParams.ToArray ();
        }
  
        private static void OnException (Exception ex)
        {
            NLog.LogManager.GetCurrentClassLogger ().Error (ex);
        }

        public static void Stop ()
        {
            if (host != null)
            {
                try
                {
                    host.Dispose ();                    
                    host = null;
                } catch {}
                System.Threading.Thread.Sleep (100);
            }
        }
    }

}
