using System;

namespace BigDataPipeline.Interfaces
{
    public class ActionLogEvent
    {
        public object Id { get; set; }

        public DateTime Date { get; set; }

        public string JobId { get; set; }

        public string Job { get; set; }

        public string Module { get; set; }

        public string Level { get; set; }

        public string Message { get; set; }

        public string Exception { get; set; }

        public ActionLogEvent ()
        {
            Date = DateTime.UtcNow;
        }

        public ActionLogEvent (string jobId, string job, string module, ActionLogLevel level, string message, string exception)
            : this ()
        {
            Job = job;
            JobId = jobId;
            Level = level.ToString();
            Module = module;
            Message = message;
            Exception = exception;
        }
    }
}
