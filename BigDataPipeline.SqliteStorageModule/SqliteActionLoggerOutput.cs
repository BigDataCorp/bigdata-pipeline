using BigDataPipeline.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using BigDataPipeline.Interfaces;

namespace BigDataPipeline.SqliteStorage
{
    /// <summary>
    /// Implements a basic output class for the action logger
    /// </summary>
    public class SqliteActionLoggerOutput : IActionLogStorage
    {
        static SimpleHelpers.SQLite.SQLiteStorage<ActionLogEvent> actionLogDb;
        
        public IEnumerable<ModuleParameterDetails> GetParameterDetails ()
        {
            yield return new ModuleParameterDetails ("workFolder", typeof (string), "Path to database files location", true);
        }

        public void GlobalInitialize (Record systemOptions)
        {
            string workFolder = systemOptions.Get ("workFolder", "");
            var fileActionDb = System.IO.Path.Combine (workFolder, "actionLog.sqlite");
            actionLogDb = new SimpleHelpers.SQLite.SQLiteStorage<ActionLogEvent> (fileActionDb, SimpleHelpers.SQLite.SQLiteStorageOptions.KeepItemsHistory ());        
        }        

        /// <summary>
        /// Writes the specified log event.
        /// </summary>
        /// <param name="evt">The log event.</param>
        public void Write (ActionLogEvent item)
        {
            actionLogDb.Set (item.JobId, item);
        }

        /// <summary>
        /// Writes the specified log event.
        /// </summary>
        /// <param name="evts">List of events.</param>
        public void Write (IList<ActionLogEvent> items)
        {
            if (items != null && items.Count > 0)
            {
                foreach (var i in items)
                    actionLogDb.Set (i.JobId, i);
            }
        }

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
        public IEnumerable<ActionLogEvent> Read (string[] jobId, string[] module, ActionLogLevel[] level, DateTime? startDate, DateTime? endDate, int? limit, int? skip, bool sortNewestFirst)
        {
            return actionLogDb.Get (sortNewestFirst).Take (limit ?? 1000);
        }

        public void Flush ()
        {
            
        }

        public void Archive (TimeSpan expiration)
        {
            actionLogDb.Remove (DateTime.UtcNow.Subtract (expiration));
        }

        public void Dispose ()
        {
            Flush ();
        }
    }
}
