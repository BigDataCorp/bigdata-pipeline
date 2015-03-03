using BigDataPipeline.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BigDataPipeline.Core
{
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
        public JobExecutionOrigin Origin { get; set; }

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

        RecordCollection[] _inputStreams;

        public void SetInputStreams (params RecordCollection[] inputStreams)
        {
            _inputStreams = inputStreams;
        }

        public RecordCollection[] GetInputStreams ()
        {
            return _inputStreams;
        }


        List<Record> _buffer;
        List<Record> _lastBuffer;

        public void Emit (Record item)
        {
            // Note: instead of a buffer, we should implement a pipeline stream of records (producer => consumer)...
            // this pipeline should be created only at the first emited item
            if (_buffer == null)
                _buffer = new List<Record> ();
            _buffer.Add (item);
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
                    Enabled = true,
                    RootAction = task
                },
                Start = DateTime.UtcNow.Add (delay ?? TimeSpan.Zero),
                Origin = JobExecutionOrigin.EmitedTask
            });
        }

        public IEnumerable<Record> GetEmitedItems ()
        {
            return _lastBuffer;
        }

        public bool HasEmitedItems ()
        {
            return _lastBuffer != null;
        }

        public void FinalizeAction (bool cleanUp = false)
        {
            _lastBuffer = cleanUp ? null : _buffer;
            _buffer = null;
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
