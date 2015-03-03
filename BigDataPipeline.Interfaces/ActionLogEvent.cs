using System;

namespace BigDataPipeline.Interfaces
{
    public class ActionLogEvent
    {
        public object Id { get; set; }

        public DateTime Date { get; set; }

        public string JobId { get; set; }

        public string Module { get; set; }

        public ActionLogLevel Level { get; set; }

        public string Message { get; set; }

        public string Exception { get; set; }

        public ActionLogEvent ()
        {
            Date = DateTime.UtcNow;
        }

        public ActionLogEvent (string jobId, string module, ActionLogLevel level, string message, string exception)
            : this ()
        {
            JobId = jobId;
            Level = level;
            Module = module;
            Message = message;
            Exception = exception;
        }
    }
}
