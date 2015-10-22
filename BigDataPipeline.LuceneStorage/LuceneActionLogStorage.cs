using BigDataPipeline.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using BigDataPipeline.Interfaces;
using Lucene.Net.Linq;
using Lucene.Net.Linq.Mapping;
using Lucene.Net.Linq.Fluent;

namespace BigDataPipeline.LuceneStorage
{
    /// <summary>
    /// Implements a basic output class for the action logger
    /// </summary>
    public class LuceneActionLogStorage : IActionLogStorage
    {
        static LuceneDataProvider provider;

        public LuceneDataProvider GetDb ()
        {
            if (provider == null)
            {
                provider = new LuceneDataProvider (Lucene.Net.Store.FSDirectory.Open (new System.IO.DirectoryInfo (path)), Lucene.Net.Util.Version.LUCENE_30);
                provider.Settings.MergeFactor = 4;
                // since we only have one type, disable filter by entity type...
                provider.Settings.EnableMultipleEntities = false;
            }
            return provider;
        }
        
        static string path;
        static IDocumentMapper<ActionLogEvent> actionLogMapper;
        
        public IEnumerable<ModuleParameterDetails> GetParameterDetails ()
        {
            yield return new ModuleParameterDetails ("workFolder", typeof (string), "Path to database files location", true);
        }

        public void GlobalInitialize (Record systemOptions)
        {
            string workFolder = systemOptions.Get ("workFolder", "");
            var fileActionDb = new System.IO.DirectoryInfo (System.IO.Path.Combine (workFolder, "lucene/actionLog/"));
            if (!fileActionDb.Exists) fileActionDb.Create ();
            path = fileActionDb.FullName;
            var map = new ClassMap<ActionLogEvent> (Lucene.Net.Util.Version.LUCENE_30);
            map.Key (i => i.Id).NotAnalyzed();
            AddPropertiesToMap (map);

            actionLogMapper = map.ToDocumentMapper ();
        }
 
        private void AddPropertiesToMap<T> (ClassMap<T> map)
        {
            var except = new HashSet<string> (map.ToDocumentMapper ().AllProperties, StringComparer.Ordinal);
            foreach (var p in typeof (T).GetProperties (System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Where (i => !except.Contains (i.Name)))
            {
                // all primitive types are IConvertible, and if the type implements this interface Lucene.Net.Linq should be able to deal with it!
                if (typeof (IConvertible).IsAssignableFrom (p.PropertyType))
                    map.AddProperty (p).NotAnalyzed ();
                else
                    map.AddProperty (p).NotIndexed ().ConvertWith (new JsonTypeConverter (p.PropertyType));
            }
        }

        /// <summary>
        /// Writes the specified log event.
        /// </summary>
        /// <param name="evt">The log event.</param>
        public void Write (ActionLogEvent item)
        {
            using (var session = GetDb ().OpenSession<ActionLogEvent> (actionLogMapper))
                session.Add (KeyConstraint.Unique, item);
        }

        /// <summary>
        /// Writes the specified log event.
        /// </summary>
        /// <param name="evts">List of events.</param>
        public void Write (IList<ActionLogEvent> items)
        {
            if (items != null && items.Count > 0)
            {
                using (var session = GetDb ().OpenSession<ActionLogEvent> (actionLogMapper))
                foreach (var i in items)
                    session.Add (KeyConstraint.Unique, i);
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
            using (var session = GetDb ().OpenSession<ActionLogEvent> (actionLogMapper))
            {
                var query = session.Query ();
                foreach (var i in query)
                    yield return i;
            }
        }

        public void Flush ()
        {
            
        }

        public void Archive (TimeSpan expiration)
        {
            using (var session = GetDb ().OpenSession<ActionLogEvent> (actionLogMapper))
            {
                var threshold = DateTime.UtcNow.Subtract (expiration);
                var list = new List<ActionLogEvent> ();
                list.AddRange (session.Query ().Where (i => i.Date < threshold).Take (250));
                while (list.Count > 0)
                {
                    foreach (var i in list)
                        session.Delete (new Lucene.Net.Search.TermQuery (new Lucene.Net.Index.Term ("Id", i.Id)));
                    list.Clear ();
                    list.AddRange (session.Query ().Where (i => i.Date < threshold).Take (250));
                }
            }
        }

        public void Dispose ()
        {
            Flush ();
        }
    }
}
