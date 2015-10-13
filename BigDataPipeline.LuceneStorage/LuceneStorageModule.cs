using BigDataPipeline.Core.Interfaces;
using BigDataPipeline.Interfaces;
using Lucene.Net.Linq;
using Lucene.Net.Linq.Fluent;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lucene.Net.Linq.Mapping;
using Newtonsoft.Json;
using System.Reflection;

namespace BigDataPipeline.LuceneStorage
{
    public class ConfigurationRecord
    {
        public string Key { get; set; }
        public string Value { get; set; }

        public ConfigurationRecord () {}
        public ConfigurationRecord (string key, string value)
        {
            Key = key; 
            Value = value;
        }
    }

    public class MockSession:ISessionContext
    {
        public string Id { get; set; }

        public PipelineJob Job { get; set; }

        public DateTime Start { get; set; }

        public string Origin { get; set; }

        public FlexibleObject Options { get; set; }

        public string Error { get; set; }

        public ActionDetails GetCurrentAction ()
        {
            // TODO: Implement this method
            throw new NotImplementedException ();
        }

        public IRecordCollection GetInputStream ()
        {
            // TODO: Implement this method
            throw new NotImplementedException ();
        }

        public IActionLogger GetLogger ()
        {
            // TODO: Implement this method
            throw new NotImplementedException ();
        }

        public IModuleContainer GetContainer ()
        {
            // TODO: Implement this method
            throw new NotImplementedException ();
        }

        public void Emit (Record item)
        {
            // TODO: Implement this method
            throw new NotImplementedException ();
        }

        public void EmitEvent (string eventName, Record item)
        {
            // TODO: Implement this method
            throw new NotImplementedException ();
        }

        public void EmitTask (ActionDetails task, TimeSpan? delay = null)
        {
            // TODO: Implement this method
            throw new NotImplementedException ();
        }
    }

    // http://stackoverflow.com/questions/12431246/derived-type-of-generic-base-class
    interface IBaseItem<out T> where T : class { }

    public class LuceneStorageItem<T>: IBaseItem<T> where T: class
    {
        private string m_value;

        private T m_item = null;

        public string _luceneType { get; set; }

        /// <summary>
        /// Date when the item was stored in database (UTC).
        /// </summary>
        public DateTime _luceneDate { get; set; }        

        /// <summary>
        /// Value is the json representation of the stored item.
        /// </summary>
        public string _luceneValue
        {
            get { return m_value; }
            set
            {
                // clear stored item instance
                if (m_value != value)
                    m_item = null;
                // set value
                m_value = value;
            }
        }

        /// <summary>
        /// The stored Item.
        /// </summary>
        public T _itemSouce
        {
            get
            {
                if (m_item == null && _luceneValue != null)
                    m_item = Newtonsoft.Json.JsonConvert.DeserializeObject<T> (_luceneValue);
                return m_item;
            }
            set
            {
                _luceneValue = Newtonsoft.Json.JsonConvert.SerializeObject (value);
                m_item = null;
            }
        }
    }

    public class JsonTypeConverter : System.ComponentModel.TypeConverter 
    {
        Type _type;

        public static JsonTypeConverter Create<T> () where T : class
        {
            return new JsonTypeConverter (typeof(T));    
        }

        public static JsonTypeConverter Create (Type type)
        {
            return new JsonTypeConverter (type);
        }

        public JsonTypeConverter (Type type)
        {
            _type = type;
        }

        public static JsonSerializerSettings DefaultSettings { get; set; }

        static JsonTypeConverter ()
        {
            DefaultSettings = new JsonSerializerSettings
            {                
                //DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                Formatting = Formatting.None,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                ObjectCreationHandling = ObjectCreationHandling.Replace
            };
        }

        public override bool CanConvertFrom (System.ComponentModel.ITypeDescriptorContext context, Type sourceType)
        {
            return true;
        }

        public override bool CanConvertTo (System.ComponentModel.ITypeDescriptorContext context, System.Type destinationType)
        {
            return true;
        }

        // Overrides the ConvertFrom method of TypeConverter.
        public override object ConvertFrom (System.ComponentModel.ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
        {
            if (value is string)
            {
                return Newtonsoft.Json.JsonConvert.DeserializeObject ((string)value, _type, DefaultSettings);
            }
            return Newtonsoft.Json.JsonConvert.SerializeObject (value);
        }
        // Overrides the ConvertTo method of TypeConverter.
        public override object ConvertTo (System.ComponentModel.ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
        {
            if (value is string)
            {
                if (destinationType == typeof (string))
                    return value;
                return Newtonsoft.Json.JsonConvert.DeserializeObject ((string)value, destinationType, DefaultSettings);
            }
            if (destinationType == typeof (string))
                return Newtonsoft.Json.JsonConvert.SerializeObject (value, DefaultSettings);
            return Newtonsoft.Json.JsonConvert.DeserializeObject (Newtonsoft.Json.JsonConvert.SerializeObject (value), destinationType, DefaultSettings);
        }
    }
    
    public class LuceneStorageModule : IStorageModule
    {
        static LuceneDataProvider collectionsDb;
        IDocumentMapper<PipelineJob> jobMapper;

        IDocumentMapper<ConfigurationRecord> configMapper;

        IDocumentMapper<ISessionContext> sessionMapper;

        public void Dispose ()
        {            
        }

        public IEnumerable<ModuleParameterDetails> GetParameterDetails ()
        {
            yield return new ModuleParameterDetails ("workFolder", typeof (string), "Path to database files location", true);
        }

        public void Initialize (Record systemOptions)
        {
            string workFolder = systemOptions.Get ("workFolder", "");
            var fileMainDb = new DirectoryInfo (System.IO.Path.Combine (workFolder, "lucene/pipelineservice/"));
            var fileActionDb = new DirectoryInfo (System.IO.Path.Combine (workFolder, "lucene/actionLog/"));

            if (!fileMainDb.Exists) fileMainDb.Create ();
            if (!fileActionDb.Exists) fileActionDb.Create ();

            if (collectionsDb == null)
            {
                collectionsDb = new LuceneDataProvider (Lucene.Net.Store.FSDirectory.Open (fileMainDb), Lucene.Net.Util.Version.LUCENE_30);
                collectionsDb.Settings.MergeFactor = 4;
                collectionsDb.Settings.EnableMultipleEntities = true;
            }

            var map = new ClassMap<PipelineJob> (Lucene.Net.Util.Version.LUCENE_30);
            map.DocumentKey ("Type").WithFixedValue ("PipelineJob");
            map.Key (i => i.Id).NotAnalyzed ();
            map.Property (i => i.Enabled).NotAnalyzed ();
            map.Property (i => i.Name).NotAnalyzed ();
            map.Property (i => i.Description).NotAnalyzed ();
            map.Property (i => i.Group).NotAnalyzed ();
            map.Property (i => i.LastExecution).NotAnalyzed ();
            map.Property (i => i.NextExecution).NotAnalyzed ();
            map.Property (i => i.Scheduler).NotIndexed ().ConvertWith (JsonTypeConverter.Create<List<string>> ());
            map.Property (i => i.RootAction).NotIndexed ().ConvertWith (JsonTypeConverter.Create<ActionDetails> ());
            map.Property (i => i.Events).NotIndexed ().ConvertWith (JsonTypeConverter.Create<HashSet<string>> ());
            map.Property (i => i.Options).NotIndexed ().ConvertWith (JsonTypeConverter.Create<Dictionary<string, string>> ());

            //AddPropertiesToMap (map);
            
            jobMapper = map.ToDocumentMapper ();

            var map2 = new ClassMap<ConfigurationRecord> (Lucene.Net.Util.Version.LUCENE_30);
            map2.DocumentKey ("Type").WithFixedValue ("Configuration");
            map2.Key (i => i.Key).NotAnalyzed ();
            map2.Property (i => i.Value).NotIndexed ();
            AddPropertiesToMap (map2);
            configMapper = map2.ToDocumentMapper ();
            
                        
            var map3 = new ClassMap<ISessionContext> (Lucene.Net.Util.Version.LUCENE_30);
            map3.DocumentKey ("Type").WithFixedValue ("SessionContext");
            map3.Key (i => i.Id).NotAnalyzed ();
            AddPropertiesToMap (map3);
            sessionMapper = map3.ToDocumentMapper ();

            Task.Run (() => ExecuteMaintenance ());
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

        private void ExecuteMaintenance ()
        {
            collectionsDb.IndexWriter.Optimize ();
        }

        public IEnumerable<PipelineJob> GetPipelineJobs (bool filterDisabledJobs = true)
        {
            using (var session = collectionsDb.OpenSession<PipelineJob> (jobMapper))
            {
                var query = session.Query ();
                if (filterDisabledJobs)
                    query = query.Where (i => i.Enabled);
                foreach (var i in query)
                    yield return i;
            }
        }

        public PipelineJob GetPipelineJob (string itemId)
        {
            using (var session = collectionsDb.OpenSession<PipelineJob> (jobMapper))
            {
                return session.Query ().FirstOrDefault (i => i.Id == itemId);
            }
        }

        public bool SavePipelineJob (PipelineJob item)
        {
            if (item.Name == null)
                item.Name = item.Id;
            using (var session = collectionsDb.OpenSession<PipelineJob> (jobMapper))
            {
                session.Add (KeyConstraint.Unique, item);
            }            
            return true;
        }

        public bool RemovePipelineJob (string itemId)
        {
            using (var session = collectionsDb.OpenSession<PipelineJob> (jobMapper))
            {
                session.Delete (new Lucene.Net.Search.TermQuery (new Lucene.Net.Index.Term ("Id", itemId)));
            }
            return true;
        }

        public Dictionary<string, string> GetConfigValues ()
        {
            var dic = new Dictionary<string, string> (StringComparer.Ordinal);
            using (var session = collectionsDb.OpenSession<ConfigurationRecord> (configMapper))
            {
                foreach (var i in session.Query ())                    
                    dic[i.Key] = i.Value;
            }
            return dic;
        }

        public string GetConfigValue (string key)
        {
            using (var session = collectionsDb.OpenSession<ConfigurationRecord> (configMapper))
            {
                var item = session.Query ().Where (i => i.Key == key).FirstOrDefault ();
                return item.Key != null ? item.Value : null;
            }            
        }

        public bool SaveConfigValue (string key, string value)
        {
            using (var session = collectionsDb.OpenSession<ConfigurationRecord> (configMapper))
            {
                session.Add (KeyConstraint.Unique, new ConfigurationRecord (key, value));
                session.Commit ();
            }
            return true;
        }

        public IEnumerable<ISessionContext> GetEnqueuedTasks ()
        {
            using (var session = collectionsDb.OpenSession<ISessionContext> (factory, sessionMapper))
            {
                var query = session.Query ();             
                foreach (var i in query)
                    yield return i;
            }
        }
        Lucene.Net.Linq.ObjectFactory<ISessionContext> factory = () =>  new MockSession ();
        public bool EnqueueTask (ISessionContext task)
        {
            using (var session = collectionsDb.OpenSession<ISessionContext> (factory, sessionMapper))
                session.Add (KeyConstraint.Unique, task);
            return true;
        }

        public bool RemoveEnqueuedTask (ISessionContext task)
        {
            using (var session = collectionsDb.OpenSession<ISessionContext> (factory, sessionMapper))
            {
                session.Delete (new Lucene.Net.Search.TermQuery (new Lucene.Net.Index.Term ("Id", task.Id)));
            }
            return true;
        }
    }
}
