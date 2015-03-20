using System;
using System.Collections.Generic;
using System.Linq;

namespace BigDataPipeline
{
    public enum SchedullerStatus { None, ShouldExecute, Reschedulled }
    
    public enum ModuleTypes { ActionModule, SystemModule }

    public class PipelineJob : FlexibleObject
    {
        // threshold for execution tolerance upper bound
        public static TimeSpan SchedulerHighThreshold = TimeSpan.FromSeconds (45);
        
        // threshold for overdue execution tolerance, must be a negative number of seconds
        public static TimeSpan SchedulerLowThreshold = TimeSpan.FromMinutes (15);

        public PipelineJob ()
        {
            Enabled = true;
        }

        private string _id;
        public string Id
        {
            get
            {
                if (_id == null || _id.Length == 0)
                    _id = Guid.NewGuid ().ToString ().Replace ("-","");
                return _id;
            }
            set { _id = value; }
        }

        /// <summary>
        /// Name of the Pipeline Job.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Domain owner of this job.
        /// </summary>
        public string Group { get; set; }

        /// <summary>
        /// Description and comments associated with this job.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Enables or disables this job.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Cron style string with execution scheduler
        /// </summary>        
        public List<string> Scheduler { get; set; }

        /// <summary>
        /// Last execution time.
        /// </summary>
        public DateTime LastExecution { get; set; }

        /// <summary>
        /// Next execution time.
        /// </summary>
        public DateTime NextExecution { get; set; }

        /// <summary>
        /// List of events types to be listened.
        /// </summary>
        /// <value>The events.</value>
        public HashSet<string> Events { get; set; }

        /// <summary>
        /// List of actions to be executed.
        /// </summary>
        public ActionDetails RootAction { get; set; }

        public bool SetScheduler (params string[] cronSchedulerString)
        {
            if (cronSchedulerString == null || cronSchedulerString.Length == 0)
            {
                Scheduler = null;
                return false;
            }
            var list = new List<string>();
            foreach (var cron in cronSchedulerString)
            {
                if (String.IsNullOrWhiteSpace (cron))
                    continue;
                try
                {
                    NCrontab.CrontabSchedule.Parse (cron.Trim ());                    
                }
                catch
                {
                    continue;
                }
                list.Add(cron.Trim ());
            }
            Scheduler = list;
            return true;
        }

        public void RecalculateScheduler()
        {
            NextExecution = CalculateNextExecution (DateTime.UtcNow);            
        }

        public void ClearScheduler ()
        {
            Scheduler = null;
            NextExecution = DateTime.MinValue;
        }

        public DateTime CalculateNextExecution (DateTime now)
        {
            if (Scheduler == null || Scheduler.Count == 0)
                return DateTime.MinValue;
            if (now == DateTime.MinValue)
                now = DateTime.UtcNow;
            return Scheduler.Select (cron => CalculateNextExecution (cron, now)).Where (dt => dt > DateTime.MinValue).DefaultIfEmpty (DateTime.MinValue).Min ();
        }

        public DateTime CalculateNextExecution (string cronExpression, DateTime now)
        {
            if (String.IsNullOrWhiteSpace (cronExpression))
                return DateTime.MinValue;
            if (now == DateTime.MinValue)
                now = DateTime.UtcNow;
            try
            {
                var cronJob = NCrontab.CrontabSchedule.Parse (cronExpression);
                // get next time that the job must be executed
                return cronJob.GetNextOccurrence (now, now.AddYears (2)).ToUniversalTime ();
            }
            catch
            {   
                return DateTime.MinValue;
            }
        }

        public IEnumerable<DateTime> CalculateNextExecutions (DateTime now)
        {
            DateTime dt;
            for (var i = 0; i < 100; i++)
            {
                dt = CalculateNextExecution (now);
                if (dt == DateTime.MinValue)
                    break;
                yield return dt;
                now = dt;
            }
        }
        
        public SchedullerStatus ShouldExecute ()
        {            
            // sanity checks
            if (!Enabled || RootAction == null)
                return SchedullerStatus.None;            

            // calculate execution time frame bounds
            DateTime now = DateTime.UtcNow;
            DateTime highThreshold = now.Add (SchedulerHighThreshold);            
            
            // check if we should execute
            if (NextExecution <= highThreshold)
            {
                DateTime lowThreshold = now.Subtract (SchedulerLowThreshold);
                // check for overdue execution
                // if overdue by less than some minutes, execute; else reschedule execution
                bool canExecute = (NextExecution > lowThreshold);

                // reschedule if overdue
                if (!canExecute)
                {
                    // check if scheduler is enabled
                    if (Scheduler == null || Scheduler.Count == 0)
                        return SchedullerStatus.None;
                    // calculate next execution
                    NextExecution = CalculateNextExecution (now);
                    // check if calculated execution is near
                    if (NextExecution <= highThreshold)
                        return SchedullerStatus.ShouldExecute;
                    return SchedullerStatus.Reschedulled;
                }
                else
                {
                    // return if should execute
                    return SchedullerStatus.ShouldExecute;
                }
            }
            return SchedullerStatus.None;
        }

        public void MarkExecutionStart ()
        {
            // update last execution
            LastExecution = DateTime.UtcNow;
            // update next execution
            NextExecution = CalculateNextExecution (LastExecution);
            if (NextExecution - LastExecution < SchedulerHighThreshold)
                NextExecution = CalculateNextExecution (LastExecution.Add (SchedulerHighThreshold));
        }
    }

    public class ActionDetails : FlexibleObject
    {
        /// <summary>
        /// Module type.
        /// </summary>
        public ModuleTypes Type { get; set; }

        /// <summary>
        /// Module full name.
        /// </summary>
        public string Module { get; set; }

        /// <summary>
        /// Followup actions in the graph.
        /// </summary>
        public List<ActionDetails> Actions { get; set; }

        /// <summary>
        /// Any comment specific for this action.
        /// </summary>
        /// <value>The comments.</value>
        public string Comments { get; set; }
    }
}



//{
//    Id: "",
//    Domain: "",
//    Description: "",
//    Enabled: true,
//    Scheduler: [],
//    Events: [],
//    RootAction: {
//        Module: "",
//        ConfigurationOptions: {},
//        Actions: []
//    }
//}