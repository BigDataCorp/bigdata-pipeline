using System;
using System.Collections;
using System.Collections.Generic;

namespace BigDataPipeline.Interfaces
{
    public struct LayoutField
    {
        public string Name { get; set; }
        public Type Type { get; set; }
        public int Index { get; set; }
    }

    public class Layout : IList<string>//IEnumerable<LayoutField>
    {
        //private List<LayoutField> _fields = new List<LayoutField> ();
        private List<string> _fields = new List<string> ();
        private Dictionary<string, int> _indexes = new Dictionary<string, int> (StringComparer.Ordinal);

        public void Add (string item)
        {
            if (!_indexes.ContainsKey (item))
            {
                lock (_fields)
                {
                    _fields.Add (item);
                    _indexes.Add (item, _fields.Count - 1);
                }
            }
        }

        //public void Add (string name, Type type)
        //{
        //    Add (new LayoutField
        //    {
        //        Name = name,
        //        Type = type
        //    });
        //}

        public void Clear ()
        {
            _indexes.Clear ();
            _fields.Clear ();
        }

        public bool Contains (string item)
        {
            return _indexes.ContainsKey (item);
        }

        public void CopyTo (string[] array, int arrayIndex)
        {
            _fields.CopyTo (array, arrayIndex);
        }

        public bool Remove (string item)
        {
            bool result = false;
            lock (_fields)
            {
                result = _fields.Remove (item);
                if (result) reindex ();
            }
            return result;
        }

        public int Count { get { return _fields.Count; } }

        public bool IsReadOnly { get; private set; }

        IEnumerator IEnumerable.GetEnumerator ()
        {
            return _fields.GetEnumerator ();
        }

        public IEnumerator<string> GetEnumerator ()
        {
            return _fields.GetEnumerator ();
        }

        public int IndexOf (string item)
        {
            int i;
            if (_indexes.TryGetValue (item, out i))
                return i;
            return -1;
        }

        public void Insert (int index, string item)
        {
            lock (_fields)
            {
                _fields.Insert (index, item);
                reindex ();
            }
        }

        public void RemoveAt (int index)
        {
            lock (_fields)
            {
                _fields.RemoveAt (index);
                reindex ();
            }
        }

        public string this[int index]
        {
            get { return _fields[index]; }
            set { _fields[index] = value; reindex (); }
        }

        public Record Create ()
        {
            return new Record (this);
        }

        private void reindex ()
        {
            _indexes.Clear ();
            for (int i = 0; i < _fields.Count; i++)
                _indexes.Add (_fields[i], i);
        }

        public Dictionary<string,int> GetIndexes ()
        {
            return _indexes;
        }
    }
}