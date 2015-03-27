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
        TimeSpan _executionQueueLowerThreshold = TimeSpan.FromMinutes (15).Negate();
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
            DateTime passStart = DateTime.UtcNow;            

            // load enqued tasks and try to execute them
            foreach (var task in _storage.GetEnqueuedTasks ())
            {
                // calculate when we should execute it!
                TimeSpan dueTime = (task.Start - passStart);
                
                // discard old/expired tasks
                if (dueTime < _executionQueueLowerThreshold)
                {
                    // remove task
                    _storage.RemoveEnqueuedTask (task);
                }
                else if (dueTime <= _executionQueueUpperThreshold)
                {
                    FireTaskExecution (new SessionContext (task), dueTime);
                    // remove task
                    _storage.RemoveEnqueuedTask (task);
                }
            }
        }

        public bool TryAddTask (SessionContext task)
        {
            if (task == null || task.Job == null || task.Job.RootAction == null)
                return false;
            if (_closed || openTasks.ContainsKey (task.Id))
                return false;
            AddTask (task);
            return true;
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
                    if (t.Value.Origin != TaskOrigin.Scheduller)
                        _storage.EnqueueTask (t.Value);
                }
            }
            openTasks.Clear ();
        }

        public void Dispose ()
        {
            Close (false);
        }

        private void AddTask (SessionContext task)
        {
            if (_closed) return;
            if (task.Start.Kind != DateTimeKind.Utc)
                task.Start = task.Start.ToUniversalTime ();
            TimeSpan dueTime = (task.Start - DateTime.UtcNow);
            if (dueTime <= _executionQueueUpperThreshold)
                FireTaskExecution (task, dueTime);
            else
                EnqueueTask (task);
        }

        private void EnqueueTask (SessionContext context)
        {
            _storage.EnqueueTask (context);
        }

        private void FireTaskExecution (SessionContext context, TimeSpan delay)
        {
            // add or overwrite task
            SessionContext existingTask;
            if (openTasks.TryGetValue (context.Id, out existingTask))
                existingTask.ClearTimerReference ();
            openTasks[context.Id] = context;

            // prepare timer
            //int dueTime = (int)(context.Start - DateTime.UtcNow).TotalMilliseconds;
            int dueTime = (int)delay.TotalMilliseconds;
            if (dueTime < 0)
                dueTime = 0;
            context.SetTimerReference (new System.Threading.Timer (IntenalExecution, context, dueTime, System.Threading.Timeout.Infinite));
        }

        private void IntenalExecution (object state)
        {  
            // try to execute task
            try
            {
                var context = (SessionContext)state;

                // remove this task from in memory waiting list
                openTasks.TryRemove (context.Id, out context);
                // release timer
                context.ClearTimerReference ();

                // special preparation for schedulled tasks
                if (context.Origin == TaskOrigin.Scheduller)
                {
                    // load collection and update its reference                
                    var savedJob = _storage.GetPipelineJob (context.Job.Id);
                    // mark schedulled task execution start
                    if (savedJob != null)
                    {
                        // if it is a scheduller generated task, the collection must be enabled to proceed
                        if (!savedJob.Enabled)
                            return;
                        savedJob.MarkExecutionStart ();
                        _storage.SavePipelineJob (savedJob);
                    }
                }
                
                // ** execute task **
                if (context.Origin == TaskOrigin.ConcurrentConsumer)
                    ExecuteAllJobActions (context, context.Job.RootAction);
                else 
                    TryToExecuteJob (context);

                // if it is a scheduller generated task, calculate next task execution
                if (context.Origin == TaskOrigin.Scheduller)
                {
                    // reload collection
                    var savedJob = _storage.GetPipelineJob (context.Job.Id);
                    // if it is a scheduller generated task, the collection must be enabled to proceed
                    if (savedJob == null || !savedJob.Enabled)
                    {
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
                                Origin = TaskOrigin.Scheduller
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error (ex);
            }           
        }

        private void TryToExecuteJob (SessionContext context)
        {
            // TODO: consider put an task queue to balance concurrent task executions...

            // put a token in the cache to sinalize the service that we have a job running
            // it is not accurate but should work in common cases. 
            // This cached token is used on the service stop request: PipelineService.Close()            
            var padLock = SimpleHelpers.MemoryCache<PipelineExecutionLock>.GetOrAdd (context.Job.Id, k => new PipelineExecutionLock { Key = k });            
            SimpleHelpers.MemoryCache<PipelineExecutionLock>.Renew (context.Job.Id);
            var currentCount = padLock.Inc ();

            // check for execution limits
            var limit = context.Job.Get ("behavior::concurrentJobExecutionsLimit", 0);
            if (limit > 0 && currentCount > limit) 
            {
                padLock.Dec ();                
                return;                
            }

            // log execution start
            _logger.Debug ("Executing collection job: {0} {1}.{2} ", context.Job.Id, context.Job.Group ?? "", context.Job.Name ?? "");

            // proceed with execution
            try
            {
                ExecuteAllJobActions (context, context.Job.RootAction);
            }
            finally
            {
                if (padLock.Dec () <= 0)
                    SimpleHelpers.MemoryCache<PipelineExecutionLock>.Remove (padLock.Key);
            }            
        }

        private void ExecuteAllJobActions (SessionContext parentContext, ActionDetails action)
        {
            // try to recursively execute all job actions
            if (action == null)
                return;

            // prepare pipeline local options
            foreach (var opt in parentContext.Job.Options)
            {
                if (!action.HasOption (opt.Key))
                    action.Options.Add (opt.Key, opt.Value);
            }

            // execute current action
            var currentContext = new SessionContext (parentContext);
            if (!ExecuteAction (currentContext, action, parentContext))
                return;

            // signal action finalization
            currentContext.FinalizeAction ();

            // next actions
            if (action.Actions != null)
            {
                for (int i = 0; i < action.Actions.Count; i++)
                {
                    var nextAction = action.Actions[i];
                    // check for internal status
                    if (nextAction.Get ("internal::status") == "ignore")
                        continue;
                    // move to next action in tree branch
                    ExecuteAllJobActions (currentContext, nextAction);
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

            // configure context
            context.SetCurrentAction (action);

            // get system options
            var svr = PipelineService.Instance;
            var systemOptions = (svr != null) ? svr.SystemOptions : new Record();

            // create logging output
            var logWriter = PipelineService.Instance.GetActionLoggerStorage ();
            var jobLogger = new ActionLogger (context.Job, action, context.Origin, logWriter, context.Options.Get ("actionLogLevel", systemOptions.Get("logLevel", ActionLogLevel.Info)), context.Options.Get ("actionLogStackTrace", false));
            context.SetLogger (jobLogger);

            // try to execute action
            try
            {
                // create module instance
                IActionModule module = null;
                switch (action.Type)
                {
                    case ModuleTypes.SystemModule:
                        {
                            module = ModuleContainer.Instance.GetInstanceAs<ISystemModule> (action.Module);
                            break;
                        }
                    case ModuleTypes.ActionModule:
                    default:
                        {
                            module = ModuleContainer.Instance.GetInstanceAs<IActionModule> (action.Module);
                            break;
                        }
                }

                // sanity check
                if (module == null)
                    throw new Exception (String.Format ("Action {0} not found", action.Module ?? "[empty]"));

                // special preparation
                if (action.Type == ModuleTypes.SystemModule)
                    ((ISystemModule)module).SetSystemParameters (_storage);

                // prepare options
                // options priority: action > job > system
                context.Options = context.Options ?? new FlexibleObject ();                
                if (systemOptions != null)
                {
                    foreach (var p in module.GetParameterDetails ())
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
                    context.SetInputStream (previousActionContext.GetEmitedItems ());

                // execute
                result = module.Execute (context);
                
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
