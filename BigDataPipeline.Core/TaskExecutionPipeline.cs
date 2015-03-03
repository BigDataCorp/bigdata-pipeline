using BigDataPipeline.Core.Interfaces;
using BigDataPipeline.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BigDataPipeline.Core
{
    public class TaskExecutionPipeline: IDisposable
    {
        private ConcurrentDictionary<string, SessionContext> openTasks = new ConcurrentDictionary<string, SessionContext> (StringComparer.Ordinal);
        IStorageModule _storage;
        
        TimeSpan _executionQueueUpperThreshold = TimeSpan.FromMinutes (5);
        TimeSpan _executionQueueLowerThreshold = TimeSpan.FromMinutes (15);
        NLog.Logger _logger = NLog.LogManager.GetLogger ("PipelineService");
        bool _closed = false;

         static TaskExecutionPipeline _instance = new TaskExecutionPipeline ();
        
        public static TaskExecutionPipeline Instance
        {
            get
            {
                return _instance;
            }
        }

        private TaskExecutionPipeline () { }

        public void Initialize (IStorageModule storage)
        {
            _closed = false;
            _storage = storage;
        }

        public void Execute ()
        {
            var threshold = DateTime.UtcNow.Add (_executionQueueUpperThreshold);
            var thresholdOld = DateTime.UtcNow.Subtract (_executionQueueLowerThreshold);
            if (_storage == null)
                return;
            foreach (var task in _storage.GetEnqueuedTasks ())
            {
                // discard old/expired tasks
                if (task.Start <= thresholdOld)
                {
                    // remove task
                    _storage.RemoveEnqueuedTask (task);
                }
                else if (task.Start <= threshold)
                {                    
                    ExecuteTask (new SessionContext (task));
                    // remove task
                    _storage.RemoveEnqueuedTask (task);
                }
            }
        }

        public bool TryAddTask (SessionContext task)
        {
            if (_closed || openTasks.ContainsKey (task.Id))
                return false;
            AddTask (task);
            return true;
        }

        private void AddTask (SessionContext task)
        {
            if (_closed) return;
            if (task.Start.Kind != DateTimeKind.Utc)
                task.Start = task.Start.ToUniversalTime ();
            if (task.Start <= DateTime.UtcNow.Add (_executionQueueUpperThreshold))
                ExecuteTask (task);
            else
                EnqueueTask (task);
        }

        private void EnqueueTask (SessionContext context)
        {
            _storage.EnqueueTask (context);
        }

        private void ExecuteTask (SessionContext context)
        {
            // add or overwrite task
            SessionContext existingTask;
            if (openTasks.TryGetValue (context.Id, out existingTask))
                existingTask.ClearTimerReference ();
            openTasks[context.Id] = context;

            // prepare timer
            int dueTime = (int)(context.Start - DateTime.UtcNow).TotalMilliseconds;
            if (dueTime < 0)
                dueTime = 0;
            context.SetTimerReference (new System.Threading.Timer (IntenalExecution, context, dueTime, System.Threading.Timeout.Infinite));
        }

        private void IntenalExecution (object state)
        {
            var context = (SessionContext)state;
            openTasks.TryRemove (context.Id, out context);            
            try
            {
                if (context == null || context.Job == null)
                    return;
                _logger.Debug ("Executing collection job: {0} {1}.{2} ", context.Job.Id, context.Job.Domain ?? "", context.Job.Name ?? "");

                // load collection and update its reference                
                var savedJob = _storage.GetPipelineCollection (context.Job.Id);
                // mark schedulled task execution start
                if (savedJob != null && context.Origin == JobExecutionOrigin.Scheduller)
                {
                    // if it is a scheduller generated task, the collection must be enabled to proceed
                    if (!savedJob.Enabled)
                        return;
                    savedJob.MarkExecutionStart ();
                    _storage.SavePipelineCollection (savedJob);
                }

                // execute task
                ExecuteJobTask (context);

                // if it is a scheduller generated task, calculate next task execution
                if (savedJob != null && context.Origin == JobExecutionOrigin.Scheduller)
                {
                    // reload collection
                    savedJob = _storage.GetPipelineCollection (context.Job.Id);
                    // if it is a scheduller generated task, the collection must be enabled to proceed
                    if (savedJob == null || !savedJob.Enabled) return;

                    // check scheduled execution
                    var execute = savedJob.ShouldExecute ();
                
                    // try to execute
                    if (execute == SchedullerStatus.ShouldExecute)
                    {
                        // set task
                        TryAddTask (new SessionContext
                        {
                            Id = savedJob.Id,
                            Job = savedJob,
                            Start = savedJob.NextExecution,
                            Origin = JobExecutionOrigin.Scheduller
                        });
                    }
                }

            }
            catch (Exception ex)
            {
                _logger.Error (ex);
            }
            finally
            {
                // release timer
                context.ClearTimerReference ();                
            }
        }

        public void Close (bool discardWaitingTasks)
        {
            // clear all waiting tasks in a double loop
            // to make sure tasks that were in the verge of execution 
            // were not dumped to disk.
            _closed = true;

            // first pass to cancel tasks
            foreach (var t in openTasks)
            {
                t.Value.ClearTimerReference ();
            }

            if (!discardWaitingTasks && _storage != null)
            {
                // second pass to dump tasks not executed to disk            
                foreach (var t in openTasks)
                {
                    if (t.Value.Origin != JobExecutionOrigin.Scheduller)
                        _storage.EnqueueTask (t.Value);
                }
            }
            openTasks.Clear ();
        }
        
        public void Dispose ()
        {
            Close (false);
        }

        private void ExecuteJobTask (SessionContext context)
        {
            // put a token in the cache to sinalize the service that we have a job running
            // it is not accurate but should work in common cases. 
            // This cached token is used on the service stop request: PipelineService.Close()
            var padLock = new PipelineExecutionLock { Key = context.Job.Id };
            SimpleHelpers.MemoryCache<PipelineExecutionLock>.Set (padLock.Key, padLock);
            try
            {
                ExecuteAllJobAction (context, context.Job.RootAction);
            }
            finally
            {
                SimpleHelpers.MemoryCache<PipelineExecutionLock>.Remove (padLock.Key);
            }
            //// allow only 100 concurrent similar jobs
            //PipelineExecutionLock padLock = null;
            //// try to execute action tree
            //try
            //{ 
            //    var key = context.Job.Id;
            //    padLock = SimpleHelpers.MemoryCache<PipelineExecutionLock>.GetOrSyncAdd (key, k => new PipelineExecutionLock { Key = k }, 20);
            //    if (padLock != null && padLock.Inc () > 100)
            //        return;
            //    SimpleHelpers.MemoryCache<PipelineExecutionLock>.Set (key, padLock);
            
            //    ExecuteAllJobAction (context, context.Job.RootAction);
            //}
            //finally
            //{
            //    if (padLock != null)
            //        padLock.Dec ();
            //}
        }

        private void ExecuteAllJobAction (SessionContext context, ActionDetails action)
        {
            // try to recursively execute all job actions
            if (action == null)
                return;

            // prepare pipeline local options
            foreach (var opt in context.Job.Options)
            {
                if (!action.HasOption (opt.Key))
                    action.Options.Add (opt.Key, opt.Value);
            }

            // current action
            if (!ExecuteAction (new SessionContext (context), action, context))
                return;

            context.FinalizeAction (action.Actions == null || action.Actions.Count == 0);

            // next actions
            if (action.Actions != null)
            {
                foreach (var nextAction in action.Actions)
                {
                    ExecuteAllJobAction (context, nextAction);
                }
            }            
        }

        /// <summary>
        /// Executes a job action.
        /// </summary>
        /// <param name="action">The action to be executed.</param>
        /// <param name="col">The pipeline collection [optional].</param>
        /// <returns></returns>
        private bool ExecuteAction (SessionContext context, ActionDetails action, SessionContext previousActionContext)
        {
            var result = false;

            var svr = PipelineService.Instance;
            var systemOptions = (svr != null) ? svr.SystemOptions : new Record();

            // create logging output
            var logWriter = PipelineService.Instance.GetActionLoggerStorage ();
            var jobLogger = new ActionLogger (context.Job, action, logWriter, context.Options.Get ("actionLogLevel", systemOptions.Get("logLevel", ActionLogLevel.Info)), context.Options.Get ("actionLogStackTrace", false));
            context.SetLogger (jobLogger);

            // create module instance
            IActionModule plugin = null;
            try
            {         
                switch (action.Type)
                {
                    case ModuleTypes.SystemModule:
                        {
                            plugin = PluginContainer.GetInstance<ISystemModule> (action.Module);
                            break;
                        }
                    case ModuleTypes.ActionModule:
                    default:
                        {
                            plugin = PluginContainer.GetInstance<IActionModule> (action.Module);
                            break;
                        }
                }

                // sanity check
                if (plugin == null)
                    throw new Exception (String.Format ("Action {0} not found", action.Module ?? "[empty]"));

                // special preparation
                if (action.Type == ModuleTypes.SystemModule)
                    ((ISystemModule)plugin).SetSystemParameters (_storage);

                // prepare options
                // options priority: action > job > system
                context.Options = context.Options ?? new FlexibleObject ();                
                if (systemOptions != null)
                {
                    foreach (var p in plugin.GetParameterDetails ())
                    {
                        context.Options.Set (p.Name, systemOptions.Get (p.Name, ""));
                    }
                }
                foreach (var p in action.Options)
                {
                    context.Options.Set (p.Key, p.Value);
                }
                // get addition parameters
                if (systemOptions != null)
                {
                    foreach (var p in context.Options.Options)
                    {
                        string key = p.Key, value = p.Value;
                        if (String.IsNullOrWhiteSpace (key))
                            continue;
                        // check for system option placeholder
                        if (value == "?")
                            context.Options.Set (key, systemOptions.Get (key, value));
                        // check for parameterized placeholder
                        else if (key[0] == '@')
                        {
                            key = key.Substring (1);
                            value = systemOptions.Get (key, "");
                            if (!String.IsNullOrWhiteSpace (value) && String.IsNullOrWhiteSpace (context.Options.Get (key)))
                                context.Options.Set (key, value);
                        }
                    }
                }

                // check for input streams
                if (previousActionContext != null && previousActionContext.HasEmitedItems ())
                    context.SetInputStreams (new RecordCollection (previousActionContext.GetEmitedItems ()));

                // execute
                result = plugin.Execute (context);
                
                // check result
                if (!result)
                {
                    // TODO: this is not needed anymore, is it?
                    if (!String.IsNullOrWhiteSpace (context.Error))
                    {
                        jobLogger.Error (context.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                jobLogger.Error (ex.Message);
                _logger.Error (ex);
            }
            
            // clean up
            if (plugin != null)
            {
                try
                {
                    plugin = null;   
                }
                catch (Exception ex)
                {
                    jobLogger.Error (ex.Message);
                    _logger.Error (ex);
                }
            }

            // process log
            jobLogger.Flush ();            
            logWriter.Dispose ();

            return result;
        }
    }

    public class PipelineExecutionLock
    {
        public string Key;
        public long Count;

        public long Inc ()
        {
            return System.Threading.Interlocked.Increment (ref Count);
        }

        public long Dec ()
        {
            return System.Threading.Interlocked.Decrement (ref Count);
        }
    }
}
