using System;

namespace BigDataPipeline.Interfaces
{    
    /// <summary>
    /// Possible action logging levels
    /// </summary>
    public enum ActionLogLevel { Trace = 0, Debug, Info, Warn, Success, Error, Fatal }

    /// <summary>
    /// Interface for logging events in a pipeline job or task
    /// </summary>
    public interface IActionLogger
    {
        ActionLogLevel GetLogLevel ();

        void Trace (string message);

        void Debug (string message);

        void Info (string message);

        void Success (string message);

        void Warn (string message);

        void Error (string message);

        void Error (Exception ex);

        void Error (string message, Exception ex);

        void Fatal (string message);

        void Fatal (Exception ex);

        void Fatal (string message, Exception ex);
    }

}
