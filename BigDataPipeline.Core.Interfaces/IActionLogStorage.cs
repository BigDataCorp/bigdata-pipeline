using BigDataPipeline.Interfaces;
using System;
using System.Collections.Generic;

namespace BigDataPipeline.Core.Interfaces
{
    /// <summary>
    /// Interface of output for the action logger
    /// </summary>
    public interface IActionLogStorage : IDisposable
    {
        /// <summary>
        /// Gets the parameter details.
        /// </summary>
        /// <returns></returns>
        IEnumerable<ModuleParameterDetails> GetParameterDetails ();

        /// <summary>
        /// Initializes the specified system options.
        /// </summary>
        /// <param name="systemOptions">The system options.</param>
        void GlobalInitialize (Record systemOptions);

        /// <summary>
        /// Writes the specified log event.
        /// </summary>
        /// <param name="evt">The log event.</param>
        void Write (ActionLogEvent evt);

        /// <summary>
        /// Writes the specified log event.
        /// </summary>
        /// <param name="evts">List of events.</param>
        void Write (IList<ActionLogEvent> evts);

        /// <summary>
        /// Reads log events in descending order of recency (newest first).<para/>
        /// The function parameter can filter the events query.
        /// </summary>
        /// <param name="jobId">If not null, will filter by job id.</param>
        /// <param name="module">If not null, will filter the module name.</param>
        /// <param name="level">If not null, will filter the event level.</param>
        /// <param name="startDate">If not null, will set a start DateTime limit.</param>
        /// <param name="endDate">If not null, will set an end DateTime limit.</param>
        /// <param name="limit">If not null, limit the result set.</param>
        /// <param name="skip">If not null, will skip the number of events. Userful for pagination.</param>
        /// <returns></returns>
        IEnumerable<ActionLogEvent> Read (string[] jobId, string[] module, ActionLogLevel[] level, DateTime? startDate, DateTime? endDate, int? limit, int? skip, bool sortNewestFirst);

        /// <summary>
        /// Flushes log events still in cache. Used by the pipeline after job execution.
        /// </summary>
        void Flush ();
    }
}
