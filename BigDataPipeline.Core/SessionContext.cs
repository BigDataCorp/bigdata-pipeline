using System.Collections.Concurrent;
using BigDataPipeline.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BigDataPipeline.Core
{
    public class TaskOrigin
    {
        public const string Scheduller = "Scheduller";
        public const string Request = "Request";
        public const string EventHandler = "EventHandler";
        public const string EmitedTask = "EmitedTask";
        public const string ConcurrentConsumer = "ConcurrentConsumer";
    }

    public class SessionContext : ISessionContext
    {        
        private string _id;

        private FlexibleObject _options;

        /// <summary>
        /// unique id for this execution.
        /// </summary>
        /// <value>The excution id.</value>
        public string Id
        {
            get
            {
                if (_id == null || _id.Length == 0)
                    _id = Guid.NewGuid ().ToString ().Replace ("-", "");
                return _id;
            }
            set { _id = value; }
        }

        /// <summary>
        /// the job.
        /// </summary>
        /// <value>The job.</value>
        public PipelineJob Job { get; set; }

        /// <summary>
        /// Start of the execution.
        /// </summary>
        /// <value>The start.</value>
        public DateTime Start { get; set; }

        /// <summary>
        /// How this action was fired.
        /// </summary>
        /// <value>The mode.</value>
        public string Origin { get; set; }

        /// <summary>
        /// Context execution options.
        /// </summary>
        /// <value>The options.</value>
        public FlexibleObject Options
        {
            get
            {
                if (_options == null)
                    _options = new FlexibleObject ();
                return _options;
            }
            set { _options = value; }
        }

        /// <summary>
        /// Error message in case of execution error.
        /// </summary>
        /// <value>The error message.</value>
        public string Error { get; set; }
        

        public SessionContext () {}

        public SessionContext (ISessionContext context)
        {
            _id = context.Id;
            Job = context.Job;
            Start = context.Start;
            Origin = context.Origin;
            Options = context.Options;
        }

        private ActionLogger _logger;

        internal void SetLogger (ActionLogger logger)
        {
            _logger = logger;    
        }

        public IActionLogger GetLogger ()
        {
            return _logger;
        }

        private bool _isTreeLeaf = true;
        private ActionDetails _concurrentConsumer;
        private ActionDetails _currentAction;

        internal void SetCurrentAction (ActionDetails currentAction)
        {
            _currentAction = currentAction;
            _isTreeLeaf = true;
            _concurrentConsumer = null;
            if (_currentAction.Actions != null && _currentAction.Actions.Count > 0)
            {
                _isTreeLeaf = false;
                _concurrentConsumer = _currentAction.Actions.FirstOrDefault (i => i.Get ("behavior::concurrentConsumer", false));
            }
        }

        public ActionDetails GetCurrentAction ()
        {
            return _currentAction;
        }

        public IModuleContainer GetContainer ()
        {
            return ModuleContainer.Instance;
        }

        IRecordCollection _inputStream;

        public void SetInputStream (IRecordCollection inputStreams)
        {
            _inputStream = inputStreams;
        }

        public IRecordCollection GetInputStream ()
        {
            return _inputStream;
        }
        
        BlockingCollection<Record> _blockingCollection;
        Action<Record> _addToBuffer;
        IRecordCollection _buffer;

        public void Emit (Record item)
        { 
            // if there is no next action to consume this emited record...
            if (_isTreeLeaf)
                return;
            // Note: instead of a buffer, we should implement a pipeline stream of records (producer => consumer)...
            // this pipeline should be created only at the first emited item
            if (_buffer == null)
            {
                InitializeInternalBuffer ();                
            }
            _addToBuffer (item);
        }
 
        private void InitializeInternalBuffer ()
        {
            if (_concurrentConsumer != null)
            {
                var limit = _concurrentConsumer.Get ("behavior::concurrentConsumerQueueLimit", 10);
                _blockingCollection = limit > 0 ? new System.Collections.Concurrent.BlockingCollection<Record> (limit) :
                                                 new System.Collections.Concurrent.BlockingCollection<Record> ();
                _addToBuffer = _blockingCollection.Add;
                _buffer = new RecordCollection (_blockingCollection.GetConsumingEnumerable ());

                // start concurrent action
                _concurrentConsumer.Set ("internal::status", "ignore");
                TaskExecutionPipeline.Instance.TryAddTask (new SessionContext
                {
                    Job = new PipelineJob
                    {
                        Id = Job.Id,
                        Name = Job.Name,
                        Group = Job.Group,
                        Enabled = true,
                        RootAction = _concurrentConsumer
                    },
                    Start = DateTime.UtcNow,
                    Origin = TaskOrigin.ConcurrentConsumer
                });
            }
            else
            {
                var col = new List<Record> ();
                _buffer = new RecordCollection (col);
                _addToBuffer = col.Add;
            }
        }

        public void EmitEvent (string eventName, Record item)
        {
            EventExecutionPipeline.Instance.FireEvent (eventName, item, Job);
        }

        public void EmitTask (ActionDetails task, TimeSpan? delay = null)
        {
            TaskExecutionPipeline.Instance.TryAddTask (new SessionContext
            {
                Job = new PipelineJob
                {
                    Id = Job.Id,
                    Name = Job.Name,
                    Group = Job.Group,
                    Enabled = true,
                    RootAction = task
                },
                Start = DateTime.UtcNow.Add (delay ?? TimeSpan.Zero),
                Origin = TaskOrigin.EmitedTask
            });
        }

        public IRecordCollection GetEmitedItems ()
        {
            return _buffer;
        }

        public bool HasEmitedItems ()
        {
            return _buffer != null;
        }

        public void FinalizeAction ()
        {
            if (_blockingCollection != null)
                _blockingCollection.CompleteAdding ();            
        }


        /// <summary>
        /// internal reference for the used timer
        /// </summary>
        private System.Threading.Timer _timer;

        /// <summary>
        /// Saves the reference for the used timer
        /// </summary>
        internal void SetTimerReference (System.Threading.Timer timer)
        {
            _timer = timer;
        }

        /// <summary>
        /// Removes any internal references for a task execution timer
        /// </summary>
        internal void ClearTimerReference ()
        {
            if (_timer != null)
            {
                try
                {
                    _timer.Dispose ();
                }
                catch
                {
                    // ignore
                }
            }
            _timer = null;
        }


    }

}
