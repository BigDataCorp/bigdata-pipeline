using BigDataPipeline.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using BigDataPipeline.Core.Interfaces;

namespace BigDataPipeline.Core
{

    public class PipelineService
    {
        static PipelineService _instance = null;

        IStorageModule _storage;
        IAccessControlModule _accessControlModule;
        string _actionLoggerOutputModuleName;

        Record _systemOptions;
        Logger _logger;
        Record _systemStatus = new Record ();

        public Record SystemOptions
        {
            get { return _systemOptions; }
        }

        public Logger Logger
        {
            get { return _logger; }
        }

        public IStorageModule GetStorage ()
        {
            return _storage;
        }

        public IActionLogStorage GetActionLoggerStorage ()
        {
            return PluginContainer.GetInstance<IActionLogStorage> (_actionLoggerOutputModuleName);
        }

        public IAccessControlModule GetAccessControlModule ()
        {
            return _accessControlModule;
        }

        public static PipelineService Instance
        {
            get { return _instance; }
        }

        public void Initialize (string pluginFolder, string workFolder, Record systemOptions, params Type[] listOfAdditionalInterfaces)
        {
            _logger = NLog.LogManager.GetLogger ("PipelineService");
            _logger.Debug ("[start] Initialization...");

            // prepare configuration
            _systemOptions = systemOptions ?? new Record ();
            _systemOptions.Set ("workFolder", workFolder);
            _systemOptions.Set ("pluginFolder", workFolder);

            // load plugins
            LoadPlugins (pluginFolder, listOfAdditionalInterfaces);

            // prepare storage
            PrepareStorage (_systemOptions);

            // prepare action logger output
            PrepareActionLoggerOutput (_systemOptions);

            // prepare access control module
            PrepareAccessControl (_systemOptions);

            // update storage configuration values
            foreach (var cfgKey in _systemOptions.Layout)
            {
                _storage.SaveConfigValue (cfgKey, _systemOptions.Get (cfgKey, ""));
            }
            // load other configuration values from storage
            foreach (var cfg in _storage.GetConfigValues ())
            {
                _systemOptions.Set (cfg.Key, cfg.Value);
            }

            // prepare pinelines
            TaskExecutionPipeline.Instance.Initialize (_storage);
            EventExecutionPipeline.Instance.Initialize (_storage);

            // set maximum lock duration for a pipeline job execution
            SimpleHelpers.MemoryCache<PipelineExecutionLock>.Expiration = TimeSpan.FromMinutes (60);

            _instance = this;

            // continue with some async initialization
            Task.Run (() => PrepareSystemPlugins ());

            _logger.Debug ("[done] Initialization...");
        }
  
        private void LoadPlugins (string pluginFolder, params Type[] listOfAdditionalInterfaces)
        {
            _logger.Debug ("[start] Loading plugins...");

            // prepare list of interfaces/plugins
            var interfaces = new Type[]
            {
                typeof(IActionModule),
                typeof(ISystemModule),
                typeof(IStorageModule),
                typeof(IAccessControlModule),
                typeof(IActionLogStorage)
            }.Concat (listOfAdditionalInterfaces).ToArray ();

            // load plugins
            PluginContainer.Initialize (pluginFolder, interfaces);

            _logger.Debug ("[done] Loading plugins...");
        }

        private void PrepareSystemPlugins ()
        {
            _logger.Debug ("[start] Preparing system plugins...");
            // load system plugins
            try
            {
                foreach (var j in PluginContainer.GetInstances<ISystemModule> ())
                {
                    try
                    {
                        var job = j.GetJobRegistrationDetails ();
                        if (job != null)
                        {
                            // save on database only if not already registered
                            if (_storage.GetPipelineCollection (job.Id) == null)
                            {
                                _storage.SavePipelineCollection (job);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error (ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error (ex);
            }
            _logger.Debug ("[done] Preparing system plugins...");
        }

        private void PrepareStorage (Record systemOptions)
        {
            _logger.Debug ("[start] Preparing storage...");

            // check configured storage
            string storageModule = systemOptions.Get ("storageModule", "");

            string storageConnectionString = systemOptions.Get ("storageConnectionString", "");
            string storageType = null;
            if (storageConnectionString != null && storageConnectionString.Contains ("://"))
                storageType = storageConnectionString.Substring (0, storageConnectionString.IndexOf ("://"));

            if (storageType == "mongodb")
                storageType = "MongoDbStorageModule";
            else if (storageType == "sqlite")
                storageType = "SqliteStorageModule";

            // try to load selected storage or clear previous selection
            _storage = (!String.IsNullOrEmpty (storageModule)) ? PluginContainer.GetInstance (storageModule) as IStorageModule : null;

            // get storage plugins
            var storagesModules = new List<string>();            
            foreach (var s in PluginContainer.GetTypes<IStorageModule> ())
            {
                storagesModules.Add (s.Name);

                // if storage already selected, skip...
                if (_storage != null)
                    continue;
                if (String.IsNullOrWhiteSpace (storageType) ||
                    s.Name.Equals (storageType, StringComparison.OrdinalIgnoreCase) ||
                    s.FullName.Equals (storageType, StringComparison.OrdinalIgnoreCase))
                {
                    _storage = PluginContainer.GetInstance(s) as IStorageModule;
                }
            }

            // set system information
            _systemStatus.Set ("storagesModules", storagesModules);
            _systemStatus.Set ("currentStoragesModule", _storage != null ? _storage.GetType().Name : "none");

            // try to initialize
            if (_storage != null)
            {
                _storage.Initialize (systemOptions);
            } 
            else 
            {
                throw new Exception ("Failed to load storage module...");
            }

            _logger.Debug ("[done] Preparing storage...");
        }

        private void PrepareActionLoggerOutput (Record systemOptions)
        {
            _logger.Debug ("[start] Preparing Action Logger Output...");

            // set default options
            if (String.IsNullOrEmpty (systemOptions.Get ("actionLoggerOutputModule", "")))
                systemOptions.Set ("actionLoggerOutputModule", "BigDataPipeline.Core.ActionLoggerInMemoryOutput");
            if (String.IsNullOrEmpty (systemOptions.Get ("actionLoggerDatabaseName", "")))
                systemOptions.Set ("actionLoggerDatabaseName", systemOptions.Get ("storageDatabaseName", ""));
            if (String.IsNullOrEmpty (systemOptions.Get ("actionLoggerConnectionString", "")))
                systemOptions.Set ("actionLoggerConnectionString", systemOptions.Get ("storageConnectionString", ""));
                    
            // check configured storage
            _actionLoggerOutputModuleName = systemOptions.Get ("actionLoggerOutputModule", "");
            // try to initialize
            using (var actionLogWriter = PluginContainer.GetInstance<IActionLogStorage> (_actionLoggerOutputModuleName))
            {
                if (actionLogWriter != null)
                {
                    actionLogWriter.GlobalInitialize (systemOptions);
                }
                else 
                {
                    throw new Exception ("Failed to load Action Logger Output module...");
                }
            }

            _logger.Debug ("[done] Preparing Action Logger Output...");
        }        

        private void PrepareAccessControl (Record systemOptions)
        {
            _logger.Debug ("[start] Preparing access control module...");

            // check configured module
            string accessControlModule = systemOptions.Get ("accessControlModule", "");            

            // get storage plugin
            var accessControlModules = new List<string> ();
            _accessControlModule = null;
            foreach (var s in PluginContainer.GetTypes<IAccessControlModule> ())
            {
                accessControlModules.Add (s.Name);
                if (_accessControlModule != null)
                    continue;
                if (String.IsNullOrWhiteSpace (accessControlModule) ||
                    s.Name.Equals (accessControlModule, StringComparison.OrdinalIgnoreCase) ||
                    s.FullName.Equals (accessControlModule, StringComparison.OrdinalIgnoreCase))
                {
                    _accessControlModule = PluginContainer.GetInstance (s) as IAccessControlModule;
                }
            }

            // set system information
            _systemStatus.Set ("accessControlModules", accessControlModules);
            _systemStatus.Set ("currentAccessControlModule", _accessControlModule != null ? _accessControlModule.GetType ().Name : "none");

            // try to initialize
            if (_accessControlModule != null)
            {
                _accessControlModule.Initialize (systemOptions);
            }
            else
            {
                _logger.Info ("No access control module was loaded...");
            }

            _logger.Debug ("[done] Preparing access control module...");
        }

        public void Close (TimeSpan wait)
        {
            if (_instance != null)
            {
                _instance = null;
                TaskExecutionPipeline.Instance.Close (false);

                if (wait.TotalMilliseconds > 0)
                {
                    DateTime start = DateTime.UtcNow;
                    // wait for execution end
                    while (SimpleHelpers.MemoryCache<PipelineExecutionLock>.Count > 0)
                    {
                        System.Threading.Thread.Sleep (250);
                        if (DateTime.UtcNow - start >= wait)
                            break;
                    }
                }

                _storage = null;
                _logger.Debug ("Closed");
            }
        }

        public void Execute ()
        {
            _logger.Debug ("Execution phase start");

            // check for storage intialization
            if (_storage == null)
            {
                PrepareStorage (_systemOptions);
                return;
            }

            // increment execution counter
            _systemStatus.Set ("excutionCount", _systemStatus.Get ("excutionCount", 0) + 1);

            // execute task internal loop
            TaskExecutionPipeline.Instance.Execute ();
            EventExecutionPipeline.Instance.StartUpdatePhase ();

            // run all exiting collections:
            // 1. check its schedulle 
            // 2. register (update) the event handlers
            foreach (var job in _storage.GetPipelineCollections ())
            {
                CheckSchedullers (job);

                // register event handlers
                EventExecutionPipeline.Instance.RegisterHandlers (job);                
            }

            EventExecutionPipeline.Instance.EndUpdatePhase ();

            _logger.Debug ("Execution phase end");
        }

        public void CheckSchedullers (PipelineJob job)
        {   
            // sanity check
            if (job == null || !job.Enabled || job.RootAction == null || String.IsNullOrEmpty (job.RootAction.Module))
                return;

            bool hasChanges = false;
                        
            // check if this action should be executed
            var execute = SchedullerStatus.None;

            // TODO: save event types (similar to registering an event listener)
            if (job.Events != null)
            {
                if (job.Events.Contains ("onStartUp") && _systemStatus.Get ("excutionCount", 0) == 1)
                {
                    job.NextExecution = DateTime.UtcNow;
                }
            }

            // check scheduled execution
            execute = job.ShouldExecute ();

            // try to execute
            if (execute == SchedullerStatus.ShouldExecute)
            {
                // set task
                TaskExecutionPipeline.Instance.TryAddTask (new SessionContext
                {
                    Id = job.Id,
                    Job = job,
                    Start = job.NextExecution,
                    Origin = JobExecutionOrigin.Scheduller
                });
            }

            // save changes
            if (execute == SchedullerStatus.Reschedulled)
            {
                hasChanges = true;
            }
           
            // save changes
            if (hasChanges)
                _storage.SavePipelineCollection (job);            
        }
    }
    
}
