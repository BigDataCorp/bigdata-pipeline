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

        public object Get (string fieldName)
        {
            if (Layout == null)
                return null;
            return Get (Layout.IndexOf (fieldName));
        }

        public object Get (int idx)
        {
            return (idx >= 0 && idx < _data.Count) ? _data[idx] : null;
        }

        public T Get<T> (string fieldName, T defaultValue)
        {
            if (Layout == null)
                return defaultValue;
            // get and convert data
            return Get (Layout.IndexOf (fieldName), defaultValue);
        }

        static Type typeConvertible = typeof (IConvertible);


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
                var type = v.GetType ();
                var desiredType = typeof (T);
                
                // we we can have a direct cast, let cast!
                if (desiredType.IsAssignableFrom(type))
                {
                    return (T)v;
                }
                // string special convertions
                else if (v is string)
                {
                    var txt = (string)v;
                    if (txt.Length == 0)
                        return defaultValue;
                    // let's deal with enums
                    if (desiredType.IsEnum)
                        return (T)Enum.Parse (desiredType, txt, true);
                    // special (more tolerant) DateTime convertion
                    // DateTime is tested prior to IConvertible, since it also implements IConvertible
                    if (desiredType == typeof (DateTime) || desiredType == typeof (DateTime?))
                    {
                        DateTime dt;
                        if (DateTime.TryParse (txt, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
                            return (T)(object)dt;
                    }
                    // all primitive types are IConvertible, 
                    // and if the type implements this interface lets use it!
                    else if (typeConvertible.IsAssignableFrom (desiredType))
                    {
                        // type convertion with InvariantCulture (faster)
                        return (T)Convert.ChangeType (txt, desiredType, System.Globalization.CultureInfo.InvariantCulture);                        
                    } 
                    // Guid doesn't implement IConvertible
                    else if (desiredType == typeof (Guid) || desiredType == typeof (Guid?))
                    {
                        Guid guid;
                        if (Guid.TryParse (txt, out guid))
                            return (T)(object)guid;
                    }
                    // TimeSpan doesn't implement IConvertible
                    else if (desiredType == typeof (TimeSpan) || desiredType == typeof (TimeSpan?))
                    {
                        TimeSpan timespan;
                        if (TimeSpan.TryParse (txt, out timespan))
                            return (T)(object)timespan;
                    }

                    // finally, fallback to json.net deserialization
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<T> (txt);
                }
                // if we want a string and have a complex object, serialize it
                else if (desiredType == typeof (string))
                {
                    // first, check if we can use a type convertion with InvariantCulture (faster)
                    if (typeConvertible.IsAssignableFrom (type))
                        return (T)Convert.ChangeType (v, desiredType, System.Globalization.CultureInfo.InvariantCulture);

                    // fallback to json.net serialization
                    return (T)(object)Newtonsoft.Json.JsonConvert.SerializeObject (v);
                }

                // else, use a type convertion with InvariantCulture (faster)
                if (typeConvertible.IsAssignableFrom (type) && typeConvertible.IsAssignableFrom (desiredType))
                    return (T)Convert.ChangeType (v, desiredType, System.Globalization.CultureInfo.InvariantCulture);
                // finally simply fallback to json.net deserialization
                return Newtonsoft.Json.JsonConvert.DeserializeObject<T> (Newtonsoft.Json.JsonConvert.SerializeObject (v));
                //return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        public void Clear ()
        {
            lock (_data) { _data.Clear (); }
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
