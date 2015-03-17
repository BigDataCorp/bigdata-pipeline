using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BigDataPipeline.Interfaces
{

    public interface IFileTransferService
    {
        IFileTransfer Open (string connectionUri, BigDataPipeline.FlexibleObject extraOptions = null);
    }

    public class StreamTransferInfo
    {
        public string FileName { get; set; }
        public Stream FileStream { get; set; }
    }

    public class FileTransferInfo
    {
        public string FileName { get; set; }
        public long Size { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
        public FileTransferInfo (string fileName, long size)
        {
            FileName = fileName;
            Size = size;
        }

        public FileTransferInfo (string fileName, long size, DateTime created, DateTime modified)
        {
            FileName = fileName;
            Size = size;
            Created = created;
            Modified = modified;
        }
    }

    /// <summary>
    /// 
    /// URI scheme for the connection string:
    /// [scheme name]://[userinfo]@[host]:[port]/[path]/[file name or wildcard expression]
    /// 
    /// ftp://[login:password@][server][:port]/[path]/[file name or wildcard expression]
    /// ftps://[login:password@][server][:port]/[path]/[file name or wildcard expression]
    /// ftpes://[login:password@][server][:port]/[path]/[file name or wildcard expression]
    /// sftp://[login:password@][server][:port]/[path]/[file name or wildcard expression]
    /// s3://[login:password@][region endpoint]/[bucketname]/[path]/[file name or wildcard expression]
    /// s3://[login:password@][s3-us-west-1.amazonaws.com]/[bucketname]/[path]/[file name or wildcard expression]
    /// http://[server][:port]/[path]/[file name or wildcard expression]
    /// https://[server][:port]/[path]/[file name or wildcard expression]
    /// c:/[path]/[file name or wildcard expression]
    /// file://[host]/[path]/[file name or wildcard expression]
    /// file:///c:/[path]
    /// 
    /// </summary>
    public class FileTransferConnectionInfo : BigDataPipeline.FlexibleObject
    {
        public const int DefaultReadBufferSize = 2 * 1024 * 1024;
        public const int DefaultWriteBufferSize = 512 * 1024;

        /// <summary>
        /// The original connection URI scheme string.
        /// </summary>
        public string ConnectionUri { get; set; }

        /// <summary>
        /// The parsed URI scheme.
        /// </summary>
        /// <value>The URI scheme.</value>
        public Uri UriScheme { get; set; }

        /// <summary>
        /// Where the file is located (scheme name).
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// If the path has a Wildcard search pattern.
        /// </summary>
        public bool HasWildCardSearch { get; set; }

        /// <summary>
        /// Regex search pattern generated from a wildcard like pattern if any.
        /// </summary>
        public string SearchPattern { get; set; }

        /// <summary>
        /// The file path without the SearchPattern.
        /// </summary>
        public string BasePath { get; set; }

        /// <summary>
        /// The full file path: unescaped AbsolutePath
        /// </summary>
        public string FullPath { get; set; }        

        /// <summary>
        /// Parsed userInfo login.
        /// </summary>
        public string Login { get; set; }

        /// <summary>
        /// Parsed userInfo password.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Host.
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// Port.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Number of retries in case of faillure
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Wait time between retries in case of faillure
        /// </summary>
        public int RetryWaitMs { get; set; }

        /// <summary>
        /// How to search for files in case of searchPattern existance
        /// </summary>
        public bool SearchTopDirectoryOnly { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileTransferConnectionInfo" /> class.
        /// </summary>
        public FileTransferConnectionInfo ()
        {
            RetryCount = 3;
            RetryWaitMs = 500;
            SearchTopDirectoryOnly = true;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileTransferConnectionInfo" /> class.
        /// </summary>
        /// <param name="connectionUri">The connection URI.</param>
        public FileTransferConnectionInfo (string connectionUri) : this ()
        {
            Parse (connectionUri);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileTransferConnectionInfo" /> class.
        /// </summary>
        /// <param name="uriScheme">The URI scheme.</param>
        public FileTransferConnectionInfo (Uri uriScheme) : this ()
        {
            Parse (uriScheme);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileTransferConnectionInfo" /> class.
        /// </summary>
        /// <param name="connectionUri">The connection URI.</param>
        /// <param name="extraOptions">The extra options.</param>
        public FileTransferConnectionInfo (string connectionUri, IEnumerable<KeyValuePair<string, string>> extraOptions) : this ()
        {
            Parse (connectionUri);
            SetExtraOptions (extraOptions);            
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileTransferConnectionInfo" /> class.
        /// </summary>
        /// <param name="uriScheme">The URI scheme.</param>
        /// <param name="extraOptions">The extra options.</param>
        public FileTransferConnectionInfo (Uri uriScheme, IEnumerable<KeyValuePair<string, string>> extraOptions) : this ()
        {
            Parse (uriScheme);
            SetExtraOptions (extraOptions);
        }
        
        /// <summary>
        /// Parses the specified connection URI.
        /// </summary>
        /// <param name="connectionUri">The connection URI.</param>
        public void Parse (string connectionUri)
        {
            Parse (new Uri (connectionUri));
        }

        /// <summary>
        /// Parses the specified URI scheme.
        /// </summary>
        /// <param name="uriScheme">The URI scheme.</param>
        public void Parse (Uri uriScheme)
        {
            UriScheme = uriScheme;
            ConnectionUri = UriScheme.OriginalString;
            Location = uriScheme.Scheme;
            Host = uriScheme.Host;
            Port = uriScheme.Port;
            FullPath = Uri.UnescapeDataString (uriScheme.AbsolutePath);

            // parse path
            HasWildCardSearch = FileTransferHelpers.HasWildcard (FullPath);
            if (HasWildCardSearch)
            {
                var p = FileTransferHelpers.SplitByWildcardPattern (FullPath);
                BasePath = p.Item1;
                SearchPattern = p.Item2;
            }
            else if (FullPath.EndsWith ("/"))
            {
                BasePath = FullPath;
                SearchPattern = "";
            }
            else
            {
                BasePath = Uri.UnescapeDataString (String.Join ("", uriScheme.Segments.Take (uriScheme.Segments.Length - 1)));
                SearchPattern = Uri.UnescapeDataString (uriScheme.Segments[uriScheme.Segments.Length - 1]);
            }

            // parse credentials
            if (!String.IsNullOrEmpty (uriScheme.UserInfo))
            {
                var credentials = uriScheme.UserInfo.Split (':');
                Login = credentials[0];
                Password = credentials.Length > 1 ? credentials[1] : null;
            }
            else
            {
                Login = null;
                Password = null;
            }
        }

        /// <summary>
        /// Sets the extra options.
        /// </summary>
        /// <param name="extraOptions">The extra options.</param>
        public void SetExtraOptions (IEnumerable<KeyValuePair<string, string>> extraOptions)
        {
            // parse extra options
            // set custom options like: sshKeyFiles (SFTP), useReducedRedundancy (S3), makePublic (S3), partSize (S3)
            if (extraOptions != null)
            {                
                foreach (var o in extraOptions)
                {
                    if (o.Value == null)
                        continue;
                    Set (o.Key, o.Value);
                }

                // parse some default extra options
                SearchTopDirectoryOnly = Get ("searchTopDirectoryOnly", SearchTopDirectoryOnly);
                RetryCount = Get ("retryCount", RetryCount);
                RetryWaitMs = Get ("retryWaitMs", RetryWaitMs);
            }
        }

        public string GetDestinationPath (string filename)
        {
            return System.IO.Path.Combine (BasePath, System.IO.Path.GetFileName (filename)).Replace ('\\', '/');
        }
    }


    public class FileTransferHelpers
    {
        public static string WildcardToRegex (string pattern)
        {
            if (pattern == null)
                return String.Empty;
            return "^" + System.Text.RegularExpressions.Regex.Escape (pattern).
                               Replace (@"\*", ".*").
                               Replace (@"\?", ".") + "$";
        }

        public static bool HasWildcard (string pattern)
        {
            if (pattern == null)
                return false;
            var wildCard1 = pattern.IndexOf ('*');
            var wildCard2 = pattern.IndexOf ('?');
            return (wildCard1 > 0 || wildCard2 > 0);
        }

        public static Tuple<string,string> SplitByWildcardPattern (string pattern)
        {
            if (pattern != null)
            {
                var wildCard1 = pattern.IndexOf ('*');
                var wildCard2 = pattern.IndexOf ('?');
                if (wildCard1 > 0 || wildCard2 > 0)
                {
                    //var parsed = new string[0];
                    int endPos = (wildCard1 > 0 && wildCard2 > 0) ? Math.Min (wildCard1, wildCard2) : Math.Max (wildCard1, wildCard2);                
                    var pos = Math.Max (pattern.LastIndexOf ('\\', endPos), pattern.LastIndexOf ('/', endPos));

                    return Tuple.Create (pos >= 0 ? pattern.Substring (0, pos) : "", pattern.Substring (endPos));
                }
            }
            return null;
        }


        public static void DeleteFile (string fileName)
        {
            if (String.IsNullOrEmpty (fileName))
                return;
            try { System.IO.File.Delete (fileName); }
            catch { }
        }

        public static void CreateDirectory (string folder)
        {
            if (String.IsNullOrEmpty (folder))
                return;
            folder = folder.Replace ('\\', '/');
            try { System.IO.Directory.CreateDirectory (System.IO.Path.GetDirectoryName (folder)); }
            catch { }
        }

        public static Encoding TryGetEncoding (string encodingName, bool useDefaultIfNotFound = true)
        {
            try
            {
                if (!String.IsNullOrEmpty (encodingName))
                    return Encoding.GetEncoding (encodingName);
            }
            catch {}
            if (useDefaultIfNotFound)
                return Encoding.GetEncoding ("ISO-8859-1");
            return null; 
        }
    }
}