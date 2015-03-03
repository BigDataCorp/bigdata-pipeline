using System;
using System.Collections.Generic;

namespace BigDataPipeline.Interfaces
{
    public enum JobExecutionOrigin
    {
        Scheduller,
        Request,
        EventHandler,
        EmitedTask
    }

    public interface ISessionContext
    {
        /// <summary>
        /// unique id for this execution.
        /// </summary>
        /// <value>The excution id.</value>
        string Id { get; set; }

        /// <summary>
        /// the job.
        /// </summary>
        /// <value>The job.</value>
        PipelineJob Job { get; set; }
        
        /// <summary>
        /// Start of the execution.
        /// </summary>
        /// <value>The start.</value>
        DateTime Start { get; set; }

        /// <summary>
        /// How this action was fired.
        /// </summary>
        /// <value>The mode.</value>
        JobExecutionOrigin Origin { get; set; }

        /// <summary>
        /// Context execution options.
        /// </summary>
        /// <value>The options.</value>
        FlexibleObject Options { get; set; }

        /// <summary>
        /// Error message in case of execution error.
        /// </summary>
        /// <value>The error message.</value>
        string Error { get; set; }

        /// <summary>
        /// Gets the input streams.
        /// </summary>
        /// <returns></returns>
        RecordCollection[] GetInputStreams ();

        /// <summary>
        /// Gets the logger.
        /// </summary>
        /// <returns></returns>
        IActionLogger GetLogger ();
                
        /// <summary>
        /// Emits the specified item.
        /// </summary>
        /// <param name="item">The item.</param>
        void Emit (Record item);

        /// <summary>
        /// Emits the event.
        /// </summary>
        /// <param name="eventName">Name of the event.</param>
        /// <param name="item">The item.</param>
        void EmitEvent (string eventName, Record item);

        /// <summary>
        /// Emits the task.
        /// </summary>
        /// <param name="task">The task.</param>
        /// <param name="delay">The delay.</param>
        void EmitTask (ActionDetails task, TimeSpan? delay = null);
    }
}
