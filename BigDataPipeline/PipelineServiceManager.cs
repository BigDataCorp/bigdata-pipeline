using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using BigDataPipeline.Core;

namespace BigDataPipeline
{
    public class PipelineServiceManager
    {
        // Service Name as will be registered in the services control manager.
        // IMPORTANT: 
        // 1. should not contains spaces or other whitespace characters
        // 2. each service on the system must have a unique name.
        public const string DefaultServiceName = "BigDataPipeline";

        // Display Name of the service in the services control manager.
        // NOTE: here the name can contains spaces or other whitespace characters
        public const string DefaultServiceDisplayName = "BigData Pipeline";
        
        // Description of the service in the services control manager.
        public const string DefaultServiceDescription = "BigData Pipeline service";
                        
        private Logger _logger = LogManager.GetLogger ("PipelineServiceManager");
        private System.Threading.Timer _runningTask = null;
        private static int _running = 0;
        private PipelineService service;
        private FlexibleOptions _options;

        public PipelineServiceManager (FlexibleOptions options)
        {
            _options = options;
        }
       
        /// <summary>
        /// Starts the service.
        /// </summary>
        public void Start ()
        {
            try
            {
                // startup setup
                Initialize ();
                
                // Star Internal timer
                StartTimer ();
                _logger.Warn ("Service start");
            }
            catch (Exception ex)
            {
                if (_logger.IsWarnEnabled && PipelineService.Instance != null)
                _logger.Warn ("last status: " + Newtonsoft.Json.JsonConvert.SerializeObject (PipelineService.Instance.SystemStatus));
                _logger.Fatal (ex);
                throw ex;
            }
        }
  
        /// <summary>
        /// Stops the service.
        /// </summary>
        public void Stop ()
        {
            try
            {
                StopTimer ();
                _logger.Warn ("Service stop: waiting for pending tasks");

                service.Close (TimeSpan.FromMinutes (20));
                _logger.Warn ("Service stopped");

                BigDataPipeline.Web.WebServer.Stop ();

                if (_logger.IsInfoEnabled && PipelineService.Instance != null)
                    _logger.Info ("last status: " + Newtonsoft.Json.JsonConvert.SerializeObject (PipelineService.Instance.SystemStatus));
            }
            catch (Exception ex)
            {
                _logger.Error (ex);
            }
            LogManager.Flush ();
        }

        /// <summary>
        /// Pauses the service by stoping the internal timer.
        /// </summary>
        public void Pause ()
        {
            StopTimer ();
        }

        /// <summary>
        /// Continues this instance by restarting the internal timer.
        /// </summary>
        public void Continue ()
        {
            StartTimer ();
        }

        /// <summary>
        /// Starts the internal timer.
        /// </summary>
        private void StartTimer ()
        { 
            // start up delay
            const int dueTime = 5000; 
            // get update interval from config
            int period = _options.Get<int> ("serviceUpdateIntervalInSeconds", 1 * 60) * 1000;
            // update task
            if (_runningTask != null)
                _runningTask.Change (period, System.Threading.Timeout.Infinite);
            else
                _runningTask = new System.Threading.Timer (UpdateEvent, this, dueTime, System.Threading.Timeout.Infinite);

            // adjust job thresholds based on execution step 
            PipelineJob.SchedulerHighThreshold = TimeSpan.FromMilliseconds (period * .75);
        }

        /// <summary>
        /// Stops the internal timer.
        /// </summary>
        private void StopTimer ()
        {
            if (_runningTask != null)
                _runningTask.Dispose ();
            _runningTask = null;
        }
                
        /// <summary>
        /// The service execution logic that is called by the timer
        /// </summary>
        /// <param name="stateInfo">The service instace reference.</param>
        public static void UpdateEvent (Object stateInfo)
        {
            var instance = (PipelineServiceManager)stateInfo;
            try
            {
                // re-start timer
                instance.StartTimer ();
                // run job
                instance.Execute ();
            }
            catch (Exception ex)
            {
                instance._logger.Error (ex);
            }
        }

        /// <summary>
        /// Initializes this instance (called once).
        /// </summary>
        private void Initialize ()
        {
            service = new PipelineService ();
            // prepare service options
            var opt = new BigDataPipeline.Interfaces.Record ();

            foreach (var c in _options.Options)
                opt.Set (c.Key, c.Value);

            // prepare module location and work areas
            var workDir = prepareFilePath (_options.Get ("workFolder", "${basedir}/work/"));
            var modulesDir = _options.Get ("modulesFolder", "").Split (',', ';', '|').Select (i => prepareFilePath (i))
                                 .Concat (_options.Get ("modules", "").Split (',', ';', '|'))
                                 .Concat (new string[] { "${basedir}/modules/" })
                                 .Where (i => !String.IsNullOrEmpty (i)).ToArray ();            
            
            (new System.IO.DirectoryInfo (workDir)).Create ();

            bool enableWebInterface = _options.Get<bool> ("webInterfaceEnabled", true);

            // initializes the service
            service.Initialize (modulesDir, workDir, opt);

            // initialize web interface (self host)
            if (enableWebInterface)
            {
                Task.Run (() => InitializeWebInterface ());
            }
        }
 
        private void InitializeWebInterface ()
        {
            // initialize web interface (self host)
            string siteRootPath = null;
#if DEBUG
            // change root path to allow dinamic update of the page content
            siteRootPath = System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory).Replace (@"BigDataPipeline\bin\Debug", "BigDataPipeline.Web");
#endif
            if (_options.Get("addUrlReservation", false))
            {
                BigDataPipeline.Web.WebServer.AddUrlReservationOnWindows("http://+:" + _options.Get("webInterfacePort", 8080) + "/" + _options.Get("webVirtualDirectoryPath", "/bigdatapipeline").Trim ('/') + '/');
            }

            BigDataPipeline.Web.WebServer.Start (_options.Get<int> ("webInterfacePort", 8080), siteRootPath, _options.Get ("webVirtualDirectoryPath", "/bigdatapipeline"), _options.Get ("webOpenFirewallExceptions", false));
            if (Environment.UserInteractive && _options.Get<bool> ("webInterfaceDisplayOnBrowserOnStart", false))
            {
                DisplayPageOnBrowser ();
            }

            //_logger.Warn (Newtonsoft.Json.JsonConvert.SerializeObject (AppDomain.CurrentDomain.GetAssemblies ().Where (a => (!a.GlobalAssemblyCache && !a.IsDynamic)).Select (i => i.FullName), Newtonsoft.Json.Formatting.Indented));
        }

        /// <summary>
        /// Adjust file path.
        /// </summary>
        private string prepareFilePath (string path)
        {
            if (path == null)
                return path;
            int ix = path.IndexOf ("${basedir}", StringComparison.OrdinalIgnoreCase);
            if (ix >= 0)
            {
                int tagLen = "${basedir}".Length;
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                if (path.Length > ix + tagLen && (path[ix + tagLen] == '\\' || path[ix + tagLen] == '/'))
                    appDir = appDir.EndsWith ("/") || appDir.EndsWith ("\\") ? appDir.Substring (0, appDir.Length - 1) : appDir;
                path = path.Remove (ix, tagLen);
                path = path.Insert (ix, appDir);                
            }
            path = path.Replace("\\", "/");
            return path;
        }

        /// <summary>
        /// Execution logic.
        /// </summary>
        /// <remarks>This may be called multiple times, so adjust in accordance</remarks>
        private bool Execute ()
        {
            const int MaxLockRetries = 5;

            // check if a previous job was executing
            if (System.Threading.Interlocked.Increment(ref _running) > 1)
            {
                _logger.Warn ("Operation already running, delaying executing {0}/{1}.", (_running - 1), MaxLockRetries);
                if (_running > MaxLockRetries)
                {
                    // disable so to force next pass execution 
                    _running = 0;
                }
                return false;
            }

            // try to execute job
            try
            {
                // execute 
                service.Execute ();
            }
            finally
            {
                _running = 0;
            }
            return true;
        }

        /// <summary>
        /// Displays the pipeline web UI on browser.
        /// </summary>
        private void DisplayPageOnBrowser ()
        {
            Task.Factory.StartNew (() =>
            {
                try
                {
                    System.Diagnostics.Process.Start (BigDataPipeline.Web.WebServer.Address);
                }
                catch (Exception ignore)
                {
                    _logger.Info (ignore);
                }
            });
        }

    }

}
