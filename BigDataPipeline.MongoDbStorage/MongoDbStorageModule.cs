using BigDataPipeline.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using BigDataPipeline.Interfaces;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace BigDataPipeline.Mongo
{
    public class MongoDbStorageModule : IStorageModule
    {
        public IEnumerable<ModuleParameterDetails> GetParameterDetails ()
        {
            yield return new ModuleParameterDetails ("storageConnectionString", typeof (string), "mongodb://[username:password@]host1[:port1][,host2[:port2],...[,hostN[:portN]]][/[database][?options]]", true);
            yield return new ModuleParameterDetails ("storageDatabaseName", typeof (string), "Name of the database for the pipeline collections. Defaults to BigdataPipeline", false);
        }

        static MongoDatabase _db;

        public void Initialize (Record systemOptions)
        {
            MongoObjectIdConverter.UseAsDefaultJsonConverter ();
            
            _db = MongoDbContext.GetDatabase (systemOptions.Get ("storageDatabaseName", "BigdataPipeline"), new MongoDB.Driver.MongoUrlBuilder (systemOptions.Get ("storageConnectionString", "")));
        }

        public void Dispose()
        {            
        }

        public static bool SafeAction (Action action, int retryCount = 2, bool throwOnError = false)
        {
            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    action ();
                    return true;
                }
                catch (Exception ex)
                {
                    if (throwOnError && i == (retryCount - 1))
                        throw;                    
                }
            }
            return false;
        }

        public static T SafeQuery<T> (Func<T> action, int retryCount = 2, bool throwOnError = false)
        {
            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    return action ();
                }
                catch (Exception ex)
                {
                    if (throwOnError && i == (retryCount - 1))
                        throw;
                }
            }
            return default (T);
        }

        public IEnumerable<PipelineJob> GetPipelineCollections (bool filterDisabledJobs = true)
        {
            var query = filterDisabledJobs ? Query.And (Query.EQ ("Enabled", true), Query.EQ ("Jobs.Enabled", true)) : Query.Null;
            return _db.GetCollection<PipelineJob> ("PipelineCollection").Find (query);
        }

        public PipelineJob GetPipelineCollection (string itemId)
        {
            return _db.GetCollection<PipelineJob> ("PipelineCollection").FindOne (Query.EQ ("_id", itemId));
        }

        public bool SavePipelineCollection (PipelineJob item)
        {
            return _db.GetCollection<PipelineJob> ("PipelineCollection").SafeSave (item);
        }

        public bool RemovePipelineCollection (string itemId)
        {
            return _db.GetCollection<PipelineJob> ("PipelineCollection").Remove (Query.EQ ("_id", itemId)).Ok;
        }

        public Dictionary<string, string> GetConfigValues ()
        {
            return _db.GetCollection ("ConfigValues").FindAll ()
                       .ToDictionary (i => i["_id"].AsString, v => v["value"].AsString, StringComparer.Ordinal);
        }

        public string GetConfigValue (string key)
        {
            var item = _db.GetCollection ("ConfigValues").FindOne (Query.EQ ("_id", key));
            return (item != null) ? item["value"].AsString : null;
        }

        public bool SaveConfigValue (string key, string value)
        {
            return _db.GetCollection ("ConfigValues").Update (Query.EQ ("_id", key), Update.Set ("value", value), UpdateFlags.Upsert).Ok;
        }

        public IEnumerable<ISessionContext> GetEnqueuedTasks ()
        {
            return _db.GetCollection<ISessionContext> ("SessionContext").FindAll ();
        }

        public bool EnqueueTask (ISessionContext task)
        {
            return _db.GetCollection<ISessionContext> ("SessionContext").SafeSave (task);
        }

        public bool RemoveEnqueuedTask (ISessionContext task)
        {
            return _db.GetCollection<ISessionContext> ("SessionContext").Remove (Query.EQ ("_id", task.Id)).Ok;
        }
    }
}
