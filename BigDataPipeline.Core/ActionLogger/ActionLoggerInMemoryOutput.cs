using BigDataPipeline.Core.Interfaces;
using BigDataPipeline.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BigDataPipeline.Core
{
    /// <summary>
    /// Implements a basic output class for the action logger
    /// </summary>
    public class ActionLoggerInMemoryOutput : IActionLogStorage
    {
        private List<ActionLogEvent> _events = new List<ActionLogEvent> ();
        
        public List<ActionLogEvent> Events
        {
            get { return _events; }
            set { _events = value; }
        }

        public IEnumerable<PluginParameterDetails> GetParameterDetails ()
        {
            // no need!
            yield break;
        }

        public void GlobalInitialize (Record systemOptions)
        {
            // no need!
        }

        /// <summary>
        /// Writes the specified log event.
        /// </summary>
        /// <param name="evt">The log event.</param>
        public void Write (ActionLogEvent evt)
        {            
            _events.Add (evt);
        }

        /// <summary>
        /// Writes the specified log event.
        /// </summary>
        public void Write (IList<ActionLogEvent> evts)
        {
            _events.AddRange (evts);
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
            var query = _events.Select (i => i);

            // set query filters
            if (jobId != null)      query = query.Where (i => jobId.Any (j => j == i.JobId));
            if (module != null)     query = query.Where (i => module.Any (j => j == i.Module));
            if (level != null)      query = query.Where (i => level.Select(j => j.ToString()).Any (j => j == i.Level));
            if (startDate.HasValue) query = query.Where (i => i.Date >= startDate.Value);
            if (endDate.HasValue)   query = query.Where (i => i.Date <= endDate.Value);
            if (skip.HasValue)      query = query.Skip (skip.Value);
            if (limit.HasValue)     query = query.Take (limit.Value);
            if (sortNewestFirst)    query = query.OrderByDescending (i => i.Date);

            // return query result
            return query;
        }

        public void Flush ()
        {
            if (Events.Count > 0)
            {
                NLog.LogManager.GetLogger ("ActionLoggerInMemoryOutput").Info (Newtonsoft.Json.JsonConvert.SerializeObject (Events,
                    Newtonsoft.Json.Formatting.Indented,
                    new Newtonsoft.Json.JsonSerializerSettings
                    {
                        NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
                        ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace
                    }));
            }
            Events.Clear ();
        }

        public void Dispose ()
        {
            Flush ();
        }
    }
}
