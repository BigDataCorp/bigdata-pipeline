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

        public Record SystemStatus
        {
            get { return _systemStatus; }
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
            return ModuleContainer.Instance.GetInstanceAs<IActionLogStorage> (_actionLoggerOutputModuleName);
        }

        public IAccessControlModule GetAccessControlModule ()
        {
            return _accessControlModule;
        }

        public static PipelineService Instance
        {
            get { return _instance; }
        }

        public void Initialize (string modulesFolder, string workFolder, Record systemOptions, params Type[] listOfAdditionalInterfaces)
        {
            _instance = this;

            _logger = NLog.LogManager.GetLogger ("PipelineService");
            _logger.Debug ("[start] Initialization...");

            // prepare configuration
            _systemOptions = systemOptions ?? new Record ();
            _systemOptions.Set ("workFolder", workFolder);
            _systemOptions.Set ("modulesFolder", modulesFolder);

            if (_logger.IsTraceEnabled)
                _logger.Trace ("System options: " + Newtonsoft.Json.JsonConvert.SerializeObject (_systemOptions));

            // load modules
            LoadModules (modulesFolder, listOfAdditionalInterfaces);

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

            // continue with some async initialization
            Task.Run (() => PrepareSystemModules ());

            _logger.Debug ("[done] Initialization...");
        }
  
        private void LoadModules (string modulesFolder, params Type[] listOfAdditionalInterfaces)
        {
            _logger.Debug ("[start] Loading modules...");

            // prepare list of interfaces/modules
            var interfaces = new Type[]
            {
                typeof(IActionModule),
                typeof(ISystemModule),
                typeof(IStorageModule),
                typeof(IAccessControlModule),
                typeof(IActionLogStorage)
            }.Concat (listOfAdditionalInterfaces).ToArray ();

            // load modules
            ModuleContainer.Instance.LoadModules (modulesFolder, interfaces);

            _logger.Debug ("[done] Loading modules...");
        }

        private void PrepareSystemModules ()
        {
            _logger.Debug ("[start] Preparing system modules...");
            // load system modules
            try
            {
                foreach (var j in ModuleContainer.Instance.GetInstancesOf<ISystemModule> ())
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
            _logger.Debug ("[done] Preparing system modules...");
        }

        private void PrepareStorage (Record systemOptions)
        {
            _logger.Debug ("[start] Preparing storage...");

            // check configured storage
            string storageModule = systemOptions.Get ("storageModule", "");

            string storageConnectionString = systemOptions.Get ("storageConnectionString", "");
            if (String.IsNullOrWhiteSpace (storageModule) && storageConnectionString != null && storageConnectionString.Contains ("://"))
                storageModule = storageConnectionString.Substring (0, storageConnectionString.IndexOf ("://"));
            if (storageModule == "mongodb")
                storageModule = "MongoDbStorageModule";
            else if (storageModule == "sqlite")
                storageModule = "SqliteStorageModule";

            // try to load selected storage or clear previous selection
            if (!String.IsNullOrWhiteSpace (storageModule))
                _storage = ModuleContainer.Instance.GetInstance (storageModule) as IStorageModule;
            else
                _storage = ModuleContainer.Instance.GetInstanceOf<IStorageModule> ();

            // get storage modules
            var storagesModules = ModuleContainer.Instance.GetTypesOf<IStorageModule> ().Select (s => s.Name).ToList ();

            // set system information
            _systemStatus.Set ("storagesModules", storagesModules);
            _systemStatus.Set ("currentStoragesModule", _storage != null ? _storage.GetType().Name : "none");

            if (_logger.IsTraceEnabled)
                _logger.Trace ("storagesModules: " + String.Join (", ", storagesModules));

            // sanity check
            if (_storage == null)
                throw new Exception ("Failed to load storage module...");

            // try to initialize
            _storage.Initialize (systemOptions);

            _logger.Debug ("[done] Preparing storage...");
        }

        private void PrepareActionLoggerOutput (Record systemOptions)
        {
            _logger.Debug ("[start] Preparing Action Logger Output...");

            // set default options
            if (String.IsNullOrWhiteSpace (systemOptions.Get ("actionLoggerOutputModule", "")))
                systemOptions.Set ("actionLoggerOutputModule", "BigDataPipeline.Core.ActionLoggerInMemoryOutput");
            if (String.IsNullOrWhiteSpace (systemOptions.Get ("actionLoggerDatabaseName", "")))
                systemOptions.Set ("actionLoggerDatabaseName", systemOptions.Get ("storageDatabaseName", ""));
            if (String.IsNullOrWhiteSpace (systemOptions.Get ("actionLoggerConnectionString", "")))
                systemOptions.Set ("actionLoggerConnectionString", systemOptions.Get ("storageConnectionString", ""));
                    
            // check configured storage
            _actionLoggerOutputModuleName = systemOptions.Get ("actionLoggerOutputModule", "");
            // try to initialize
            using (var actionLogWriter = ModuleContainer.Instance.GetInstanceAs<IActionLogStorage> (_actionLoggerOutputModuleName))
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
            string accessControlModuleName = systemOptions.Get ("accessControlModule", "");            

            // get storage module
            var accessControlModules = ModuleContainer.Instance.GetTypesOf<IAccessControlModule> ().Select (s => s.Name).ToList ();

            // try to load by name or get the first available module
            if (!String.IsNullOrWhiteSpace (accessControlModuleName))
                _accessControlModule = ModuleContainer.Instance.GetInstance (accessControlModuleName) as IAccessControlModule;
            else
                _accessControlModule = ModuleContainer.Instance.GetInstanceOf<IAccessControlModule> ();
            
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
            _logger.Trace ("Execution phase start");

            // check for storage intialization
            if (_storage == null)
            {
                _logger.Debug ("Storage not initalized... aborting execution phase");
                return;
            }

            // increment execution counter
            _systemStatus.Set<long> ("excutionCount", _systemStatus.Get<long> ("excutionCount", 0) + 1);

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

            _logger.Trace ("Execution phase end");
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
                    Origin = TaskOrigin.Scheduller
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
