using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BigDataPipeline.Web.Models
{
    public class QueryRequest
    {
        public string op { get; set; }
        public Dictionary<string, string> data { get; set; }

        public bool HasData (string key)
        {
            return data != null && data.ContainsKey (key);
        }

        public string GetData (string key)
        {
            return GetData<string> (key, String.Empty);
        }

        public T GetData<T> (string key, T defaultValue = default(T))
        {
            if (data == null)
                return defaultValue;

            string v;
            if (key != null && data.TryGetValue (key, out v))
            {
                try
                {
                    if (v == null || v.Length == 0)
                        return defaultValue;

                    bool missingQuotes = (v.Length > 0 && !(v[0] == '\"' && v[v.Length - 1] == '\"'));

                    if (typeof (T) == typeof (string))
                    {
                        if (missingQuotes)
                            return (T)(object)v;
                        return Newtonsoft.Json.JsonConvert.DeserializeObject<T> (v);
                    }
                    else if (!missingQuotes)
                    {
                        v = v.Substring (1, v.Length - 2);
                    }
                    // else, use a type convertion with InvariantCulture (faster)
                    if (typeof (T).IsPrimitive)
                        return (T)Convert.ChangeType (v, typeof (T), System.Globalization.CultureInfo.InvariantCulture);
                    // finally, deserialize it!
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<T> (v);

                }
                catch { /* ignore and return default value */ }
            }
            return defaultValue;
        }

        public QueryRequest SetData<T> (string key, T value)
        {
            if (key != null)
            {
                if (data == null)
                    data = new Dictionary<string, string> (StringComparer.Ordinal);

                if (typeof (T) == typeof (string))
                {
                    data[key] = (string)(object)value;
                }
                else if (typeof (T).IsPrimitive)
                {
                    data[key] = (string)Convert.ChangeType (value, typeof (string), System.Globalization.CultureInfo.InvariantCulture);
                }
                else
                {
                    data[key] = Newtonsoft.Json.JsonConvert.SerializeObject (value);
                }
            }
            return this;
        }
    }
}
