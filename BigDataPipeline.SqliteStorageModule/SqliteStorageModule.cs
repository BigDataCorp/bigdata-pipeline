using BigDataPipeline.Core.Interfaces;
using BigDataPipeline.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BigDataPipeline.SqliteStorage
{
    public class SqliteStorageModule : IStorageModule
    {
        SimpleHelpers.SQLite.SQLiteStorage<PipelineJob> collectionsDb;
        SimpleHelpers.SQLite.SQLiteStorage<string> configDb;

        SimpleHelpers.SQLite.SQLiteStorage<ISessionContext> enqueuedTasksDb;

        public void Dispose ()
        {            
        }

        public IEnumerable<PluginParameterDetails> GetParameterDetails ()
        {
            yield return new PluginParameterDetails ("workFolder", typeof (string), "Path to database files location", true);
        }

        public void Initialize (Record systemOptions)
        {
            string workFolder = systemOptions.Get ("workFolder", "");
            var fileMainDb = System.IO.Path.Combine (workFolder, "pipelineservice.sqlite");
            var fileActionDb = System.IO.Path.Combine (workFolder, "actionLog.sqlite");

            collectionsDb = new SimpleHelpers.SQLite.SQLiteStorage<PipelineJob> (fileMainDb, SimpleHelpers.SQLite.SQLiteStorageOptions.UniqueKeys ());

            configDb = new SimpleHelpers.SQLite.SQLiteStorage<string> (fileMainDb, "pipelineconfig", SimpleHelpers.SQLite.SQLiteStorageOptions.UniqueKeys ());

            enqueuedTasksDb = new SimpleHelpers.SQLite.SQLiteStorage<ISessionContext> (fileMainDb, SimpleHelpers.SQLite.SQLiteStorageOptions.KeepUniqueItems ());

            Task.Run (() => ExecuteMaintenance ());
        }

        private void ExecuteMaintenance ()
        {
            collectionsDb.Shrink ();
        }

        public IEnumerable<PipelineJob> GetPipelineCollections (bool filterDisabledJobs = true)
        {
            // load items
            IEnumerable<PipelineJob> list = collectionsDb.Get (false);
            // filter
            if (filterDisabledJobs)
                return list.Where (i => i.Enabled);
            else
                return list;
        }

        public PipelineJob GetPipelineCollection (string itemId)
        {
            return collectionsDb.Get (itemId).FirstOrDefault ();
        }

        public bool SavePipelineCollection (PipelineJob item)
        {
            if (item.Name == null)
                item.Name = item.Id;
            collectionsDb.Set (item.Id, item);
            return true;
        }

        public bool RemovePipelineCollection (string itemId)
        { 
            collectionsDb.Remove (itemId);
            return true;
        }

        public Dictionary<string, string> GetConfigValues ()
        {
            var dic = new Dictionary<string, string> (StringComparer.Ordinal);
            foreach (var i in configDb.GetDetails (false))
                dic.Add (i.Key, i.Item);
            return dic;
        }

        public string GetConfigValue (string key)
        {            
            var item = configDb.GetDetails (key).FirstOrDefault ();
            return item != null ? item.Value : null;
        }

        public bool SaveConfigValue (string key, string value)
        {
            configDb.Set (key, value);
            return true;
        }

        public IEnumerable<ISessionContext> GetEnqueuedTasks ()
        {
            return enqueuedTasksDb.Get ();
        }

        public bool EnqueueTask (ISessionContext task)
        {
            enqueuedTasksDb.Set (task.Id, task);
            return true;
        }

        public bool RemoveEnqueuedTask (ISessionContext task)
        {
            enqueuedTasksDb.Remove (task.Id);
            return true;
        }
    }
}
