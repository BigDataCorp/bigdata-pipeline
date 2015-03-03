using Nancy;
using System;
using System.Globalization;

namespace BigDataPipeline.Web
{
    // http://simoncropp.com/conditionalresponseswithnancyfx
    // http://stackoverflow.com/questions/12726113/does-nancyfx-support-static-content-caching-via-the-etag-and-last-modified-heade
    public static class NancyModuleExtensions
    {
        public static T Convert<T> (this DynamicDictionaryValue value, T defaultValue)
        {
            if (value == null || !value.HasValue)
                return defaultValue;
            string content = value.ToString ();
            //if (obj is string)
            //    content = (string)obj;
            //else
            //    content = obj.ToString ();//Newtonsoft.Json.JsonConvert.SerializeObject (obj);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T> (content);
        }

        public static void RegisterCacheCheck (this NancyModule nancyModule)
        {
            nancyModule.After.AddItemToEndOfPipeline (CheckForCached);
        }

        static void CheckForCached (NancyContext context)
        {
            // sanity check
            if (context == null || context.Request == null)
            {
                return;
            }

            var requestEtag = String.Join ("", context.Request.Headers.IfNoneMatch);
            var requestDate = context.Request.Headers.IfModifiedSince;
            bool isCached = false;
            var responseHeaders = context.Response.Headers;

            string etag;
            if (responseHeaders.TryGetValue ("ETag", out etag))
            {
                if (requestEtag != null && !string.IsNullOrEmpty (etag) && requestEtag.IndexOf (etag, StringComparison.Ordinal) >= 0)
                {
                    isCached = true;
                }
            }

            if (requestDate.HasValue && !isCached)
            {
                string responseLastModifiedString;
                if (responseHeaders.TryGetValue ("Last-Modified", out responseLastModifiedString))
                {
                    if (responseLastModifiedString != null && responseLastModifiedString.Length > 2)
                    {
                        DateTime responseLastModified;
                        if (DateTime.TryParseExact (responseLastModifiedString, "R", CultureInfo.InvariantCulture, DateTimeStyles.None, out responseLastModified))
                        {
                            if (responseLastModified == DateTime.MinValue || ((int)(responseLastModified - requestDate.Value).TotalSeconds) <= 0)
                            {
                                isCached = true;
                            }
                        }
                    }
                }
            }


            if (isCached)
            {
                context.Response.StatusCode = HttpStatusCode.NotModified;
                context.Response.ContentType = null;
                context.Response.Contents = Response.NoBody;
            }
        }
    }
}