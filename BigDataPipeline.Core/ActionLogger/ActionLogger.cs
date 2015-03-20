using BigDataPipeline.Core.Interfaces;
using BigDataPipeline.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BigDataPipeline.Core
{
    public class ActionLogger : IActionLogger
    {
        static readonly HashSet<string> tracingLogTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "TRACE", "DEBUG", "TRACING" };

        PipelineJob _job;
        ActionDetails _action;
        bool _logExceptionStackTrace = true;
        ActionLogLevel _minLogLevel;
        IActionLogStorage _writer;
        string _origin;

        public ActionLogger (PipelineJob job, ActionDetails action , string origin, IActionLogStorage writer, ActionLogLevel minLogLevel, bool logExceptionStackTrace)
        {
            if (job == null)
                throw new ArgumentNullException ("job");
            if (action == null)
                throw new ArgumentNullException ("action");
            if (writer == null)
                throw new ArgumentNullException ("writer");


            _job = job;
            _action = action;
            _origin = origin.ToString ();
            _minLogLevel = minLogLevel;
            _logExceptionStackTrace = logExceptionStackTrace;

            _writer = writer;
        }

        internal void Flush ()
        {
            _writer.Flush ();
        }

        public ActionLogLevel GetLogLevel ()
        {
            return _minLogLevel;
        }

        public void Trace (string message)
        {
            if (_minLogLevel <= ActionLogLevel.Trace)
                Log (ActionLogLevel.Trace, message);
        }

        public void Debug (string message)
        {
            if (_minLogLevel <= ActionLogLevel.Debug)
                Log (ActionLogLevel.Debug, message);
        }

        public void Info (string message)
        {
            if (_minLogLevel <= ActionLogLevel.Info)
                Log (ActionLogLevel.Info, message);
        }

        public void Success (string message)
        {
            if (_minLogLevel <= ActionLogLevel.Success)
                Log (ActionLogLevel.Success, message);
        }

        public void Warn (string message)
        {
            if (_minLogLevel <= ActionLogLevel.Warn)
                Log (ActionLogLevel.Warn, message);
        }

        public void Error (string message)
        {
            Error (message, null);
        }

        public void Error (Exception ex)
        {
            if (ex != null)
                Error (ex.GetType ().Name, ex);
        }

        public void Error (string message, Exception ex)
        {
            if (_minLogLevel <= ActionLogLevel.Error)
                Log (ActionLogLevel.Error, message, ex);
        }

        public void Fatal (string message)
        {
            Fatal (message, null);
        }

        public void Fatal (Exception ex)
        {
            if (ex != null)
                Fatal (ex.GetType ().Name, ex);
        }

        public void Fatal (string message, Exception ex)
        {
            Log (ActionLogLevel.Fatal, message, ex);
        }

        private void Log (ActionLogLevel level, string message, Exception exception = null)
        {
            _writer.Write (new ActionLogEvent (_job, _action, _origin, level, message, GetExceptionAsText (exception, _logExceptionStackTrace)));
        }

        private static string GetExceptionAsText (Exception ex, bool includeStackTrace)
        {
            if (ex == null || ex.Message == null)
                return null;
            if (includeStackTrace)
                return ex.ToString ();
            string txt = "[" + ex.GetType().Name + "] " +  ex.Message + ".";            
            if (ex.InnerException != null)
            {
                txt += " [Inner] " + GetExceptionAsText (ex.InnerException, includeStackTrace);
            }
            return txt;
        }
    }

}
