using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BigDataPipeline
{
    public class FlexibleOptions
    {
        private Dictionary<string, string> _options;

        /// <summary>
        /// Internal dictionary with all options.
        /// </summary>
        public Dictionary<string, string> Options
        {
            get
            {
                if (_options == null)
                    _options = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);
                return _options;
            }
            set { _options = value; }
        }

        /// <summary>
        /// Check if a key exists
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool HasOption (string key)
        {
            return Options.ContainsKey (key);
        }

        /// <summary>
        /// Add/Overwrite and option. The value will be serialized to string.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public FlexibleOptions Set<T> (string key, T value)
        {
            if (key != null)
            {
                bool isNull = (value as object == null);
                if (typeof (T) == typeof (string) && !isNull)
                {
                    Options[key] = (string)(object)value;
                }
                else if (typeof (T).IsPrimitive)
                {
                    Options[key] = (string)Convert.ChangeType (value, typeof (string), System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (!isNull)
                {
                    Options[key] = Newtonsoft.Json.JsonConvert.SerializeObject (value);
                }
            }
            return this;
        }

        /// <summary>
        /// Get the option as string. If the key doen't exist, an Empty string is returned.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string Get (string key)
        {
            return Get<string> (key, String.Empty);
        }

        /// <summary>
        /// Get the option as the desired type.
        /// If the key doen't exist or the type convertion fails, the provided defaultValue is returned.
        /// The type convertion uses the Json.Net serialization to try to convert.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public T Get<T> (string key, T defaultValue)
        {
            string v;
            if (key != null && Options.TryGetValue (key, out v))
            {
                try
                {
                    if (v == null || v.Length == 0)
                        return defaultValue;

                    bool missingQuotes = v.Length < 2 || (!(v[0] == '\"' && v[v.Length - 1] == '\"'));
                    var desiredType = typeof (T);

                    if (desiredType == typeof (string))
                    {
                        if (missingQuotes)
                            return (T)(object)v;
                        return Newtonsoft.Json.JsonConvert.DeserializeObject<T> (v);
                    }
                    // else, use a type convertion with InvariantCulture (faster)
                    else if (desiredType.IsPrimitive)
                    {
                        if (!missingQuotes)
                            v = v.Substring (1, v.Length - 2);
                        return (T)Convert.ChangeType (v, typeof (T), System.Globalization.CultureInfo.InvariantCulture);
                    }
                    // more comprehensive datetime parser, except formats like "\"\\/Date(1335205592410-0500)\\/\""
                    else if (desiredType == typeof (DateTime) || desiredType == typeof (DateTime?))
                    {
                        DateTime dt;
                        var vDt = missingQuotes ? v : v.Substring (1, v.Length - 2);
                        if (DateTime.TryParse (vDt, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
                            return (T)(object)dt;
                        if (vDt.Length == 8 && DateTime.TryParseExact (vDt, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
                            return (T)(object)dt;
                        return Newtonsoft.Json.JsonConvert.DeserializeObject<T> (v);
                    }
                    else if (desiredType == typeof (Guid) || desiredType == typeof (Guid?))
                    {
                        Guid guid;
                        if (Guid.TryParse (v, out guid))
                            return (T)(object)guid;
                    }
                    else if (desiredType == typeof (TimeSpan) || desiredType == typeof (TimeSpan?))
                    {
                        TimeSpan timespan;
                        if (TimeSpan.TryParse (Convert.ToString (v), out timespan))
                            return (T)(object)timespan;
                    }

                    // finally, deserialize it!                   
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<T> (v);
                }
                catch { /* ignore and return default value */ }
            }
            return defaultValue;
        }

        /// <summary>
        /// Merge together FlexibleObjects, the last object in the list has priority in conflict resolution (overwrite).
        /// </summary>
        /// <param name="items"></param>
        /// <returns></returns>
        public static FlexibleOptions Merge (params FlexibleOptions[] items)
        {
            var merge = new FlexibleOptions ();
            if (items != null)
            {
                foreach (var i in items)
                {
                    if (i == null)
                        continue;
                    foreach (var o in i.Options)
                    {
                        merge.Options[o.Key] = o.Value;
                    }
                }
            }
            return merge;
        }
    }
}
