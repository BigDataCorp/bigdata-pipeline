using BigDataPipeline.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using BigDataPipeline.Interfaces;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace BigDataPipeline.Mongo
{
    /// <summary>
    /// Implements a basic output class for the action logger
    /// </summary>
    public class MongoDbActionLoggerOutput : IActionLogStorage
    {
        public IEnumerable<ModuleParameterDetails> GetParameterDetails ()
        {
            yield return new ModuleParameterDetails ("actionLoggerConnectionString", typeof (string), "mongodb://[username:password@]host1[:port1][,host2[:port2],...[,hostN[:portN]]][/[database][?options]]", true);
            yield return new ModuleParameterDetails ("actionLoggerDatabaseName", typeof (string), "Name of the database for the pipeline collections. Defaults to BigdataPipeline", false);
        }

        static MongoDatabase _db;

        public void GlobalInitialize (Record systemOptions)
        {
            MongoObjectIdConverter.UseAsDefaultJsonConverter ();

            _db = MongoDbContext.GetDatabase (systemOptions.Get ("actionLoggerDatabaseName", "BigdataPipeline"), new MongoDB.Driver.MongoUrlBuilder (systemOptions.Get ("actionLoggerConnectionString", "")));
        }

        /// <summary>
        /// Writes the specified log event.
        /// </summary>
        /// <param name="evt">The log event.</param>
        public void Write (ActionLogEvent item)
        {
            if (item.Id == null)
                item.Id = MongoDB.Bson.ObjectId.GenerateNewId ();
            _db.GetCollection<ActionLogEvent> ("ActionLog").SafeSave (item);
        }

        /// <summary>
        /// Writes the specified log event.
        /// </summary>
        /// <param name="evts">List of events.</param>
        public void Write (IList<ActionLogEvent> items)
        {
            if (items != null && items.Count > 0)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].Id == null)
                        items[i].Id = MongoDB.Bson.ObjectId.GenerateNewId ();
                }
                _db.GetCollection<ActionLogEvent> ("ActionLog").SafeInsertBatch (items);
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
            var query = new List <IMongoQuery> ();

            // set query filters
            if (jobId != null)      query.Add (Query.In("JobId", new MongoDB.Bson.BsonArray (jobId)));
            if (module != null)     query.Add (Query.In("Module", new MongoDB.Bson.BsonArray (module)));
            if (level != null)      query.Add (Query.In("Level", new MongoDB.Bson.BsonArray (level)));
            if (startDate.HasValue) query.Add (Query.GTE ("Date", startDate.Value));
            if (endDate.HasValue)   query.Add (Query.LTE ("Date", endDate.Value));
            
            var cursor = _db.GetCollection<ActionLogEvent> ("ActionLog").Find (query.Count > 0 ? Query.And (query) : Query.Null);

            if (skip.HasValue)      cursor.SetSkip (skip.Value);
            if (limit.HasValue)     cursor.SetLimit (limit.Value);            
            if (sortNewestFirst)    cursor.SetSortOrder (SortBy.Descending ("Date"));
            else                    cursor.SetSortOrder (SortBy.Ascending ("Date"));

            // return query result
            return cursor;
        }

        public void Flush ()
        {
            
        }

        public void Dispose ()
        {
            Flush ();
        }
    }
}
