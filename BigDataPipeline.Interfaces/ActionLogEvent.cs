using System;

namespace BigDataPipeline.Interfaces
{
    public class ActionLogEvent
    {
        public object Id { get; set; }

        public DateTime Date { get; set; }

        public string Origin { get; set; }

        public string JobId { get; set; }

        public string Job { get; set; }

        public string Group { get; set; }

        public string Module { get; set; }

        public string Level { get; set; }

        public string Message { get; set; }

        public string Exception { get; set; }

        public ActionLogEvent ()
        {
            Date = DateTime.UtcNow;
        }

        public ActionLogEvent (PipelineJob job, ActionDetails action, string origin, ActionLogLevel level, string message, string exception)
        {
            Date = DateTime.UtcNow;
            JobId = job.Id;
            Job = job.Name;            
            Group = job.Group;
            Module = action.Module;
            Origin = origin;
            Level = level.ToString ();            
            Message = message;
            Exception = exception;
        }
    }
}
