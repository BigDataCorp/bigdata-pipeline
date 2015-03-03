using System;
using System.Collections;
using System.Collections.Generic;

namespace BigDataPipeline.Interfaces
{
    public class RecordCollection : IEnumerable<Record>
    {
        public Layout Layout { get; set; }

        IEnumerable<Record> _records;

        public RecordCollection ()
        {
        }

        public RecordCollection (IEnumerable<Record> records)
        {
            _records = records;
        }

        public void SetStream (IEnumerable<Record> records)
        {
            _records = records;
        }

        public IEnumerable<Record> GetStream ()
        {
            return _records;
        }

        IEnumerator IEnumerable.GetEnumerator ()
        {
            return _records.GetEnumerator ();
        }

        public IEnumerator<Record> GetEnumerator ()
        {
            return _records.GetEnumerator ();
        }
    }

    public class Record : System.Runtime.Serialization.ISerializable
    {
        private List<object> _data = null;

        private Layout _layout = null;

        public Record ()
        {
            _data = new List<object> ();
        }

        public Record (Layout layout)
        {            
            _data = new List<object> (layout.Count);
            Layout = layout;
        }

        protected Record (System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
        {
            _data = new List<object> (info.MemberCount);
            foreach (var entry in info)
            {
                Set (entry.Name, entry.Value);
            }            
        }

        public void GetObjectData (System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
        {
            if (_layout == null)
                return;
            foreach (var e in _layout.GetIndexes ())
            {
                info.AddValue (e.Key, _data[e.Value]);
            }
        }

        public List<object> Data
        {
            get { return _data; }
            set { _data = value; }
        }

        public object this[int index]
        {
            get { return _data[index]; }
            set { _data[index] = value; }
        }

        public object this[string fieldName]
        {
            get { return Get (fieldName); }
            set { Set (fieldName, value); }
        }

        public Layout Layout
        {
            get { return _layout; }
            set { _layout = value; }
        }

        public int Count { get { return _data.Count; } }

        
        public Record Set<T> (string fieldName, T value)
        {
            // create a layout if there is none
            if (Layout == null)
            {
                Layout = new Layout ();
                Layout.Add (fieldName);
            }

            // get layout index position
            int idx = Layout.IndexOf (fieldName);
            if (idx < 0)
            {
                Layout.Add (fieldName);
                idx = Layout.Count - 1;
            }

            // set value
            return Set (idx, value);
        }

        public Record Set<T> (int idx, T value)
        {
            // set data
            lock (_data)
            {
                if (idx == _data.Count)
                {
                    _data.Add (value);                
                }
                else if (idx > _data.Count)
                {
                    for (int i = _data.Count; i < idx; i++)
                        _data.Add (null);
                    _data.Add (value);                
                }
                else
                {
                    _data[idx] = value;                
                }
            }
            return this;
        }

        public T Get<T> (string fieldName)
        {
            return Get<T> (fieldName, default(T));
        }

        public T Get<T> (string fieldName, T defaultValue)
        {
            if (Layout == null)
                return defaultValue;
            int idx = Layout.IndexOf (fieldName);
            // get and convert data
            return Get (idx, defaultValue);            
        }

        public T Get<T> (int idx, T defaultValue)
        {
            // get data
            object v = Get (idx);
            // check for default value
            if (v == null)
                return defaultValue;

            // try to convert data
            // if same type return
            if (v is T)
                return (T)v;
            try
            {
                // if we have a string and a json object, deserialize it
                if (v is string)
                {
                    var txt = (string)v;
                    if (txt.Length > 0 && txt[0] == '{')
                        return Newtonsoft.Json.JsonConvert.DeserializeObject<T> (txt);
                }
                // if we want a string and have a complex object, serialize it
                else if (typeof (T) == typeof (string) && !v.GetType ().IsPrimitive)
                {
                    return (T)(object)Newtonsoft.Json.JsonConvert.SerializeObject (v);
                }

                // else, use a type convertion with InvariantCulture (faster)
                return (T)Convert.ChangeType (v, typeof (T), System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        public object Get (string fieldName)
        {
            if (Layout == null)
                return null;
            int idx = Layout.IndexOf (fieldName);            
            return Get(idx);
        }

        public object Get (int idx)
        {
            object v = null;
            if (idx >= 0 && idx < _data.Count)
            {
                v = _data[idx];
            }
            return v;
        }

        public void Clear ()
        {
            _data.Clear ();
        }

        public void CopyFrom (Record source)
        {
            var layout = source.Layout;
            for (int i = 0; i < layout.Count; i++)
                Set (layout[i], source.Data[i]);
        }

        public IEnumerable<object> Items ()
        {
            //for (var i = 0; i < _data.Count; ++i)
            //    yield return _data[i];
            return _data;
        }

        public IEnumerator<object> GetEnumerator ()
        {
            return _data.GetEnumerator ();
        }

    }
}
