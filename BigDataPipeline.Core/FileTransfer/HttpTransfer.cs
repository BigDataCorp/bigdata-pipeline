using BigDataPipeline.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace BigDataPipeline.Core
{
    public class HttpTransfer : IFileTransfer
    {
        HttpClient _client = null;

        static string[] serviceSchemes = new[] { "http", "https" };

        /// <summary>
        /// Gets the URI scheme names that this intance can handle.
        /// </summary>
        public IEnumerable<string> GetSchemeNames ()
        {
            return serviceSchemes;
        }

        public FileTransferConnectionInfo ParseConnectionUri (string connectionUri, IEnumerable<KeyValuePair<string, string>> extraOptions)
        {
            return new FileTransferConnectionInfo (connectionUri, extraOptions);
        }

        public FileTransferConnectionInfo Details { get; private set; }

        public bool Status { get; private set; }

        public string LastError { get; private set; }

        private void _setStatus (Exception message)
        {
            string msg = null;
            if (message != null && message.Message != null)
            {
                msg = message.Message;
                if (message.InnerException != null && message.InnerException.Message != null)
                    msg += "; " + message.InnerException.Message;
            }

            _setStatus (msg != null, msg);
        }

        private void _setStatus (bool status, string message = null)
        {
            Status = status;
            LastError = message;
        }

        public bool Open (FileTransferConnectionInfo details)
        {
            Details = details;
            if (Details.RetryCount <= 0)
                Details.RetryCount = 1;
            return true;
        }

        private string PreparePath (string folder)
        {
            if (String.IsNullOrEmpty (folder))
                folder = "/";           
            return folder;
        }

        public bool IsOpened ()
        {
            return true;
        }

        public void Dispose ()
        {    
            if (_client != null)
                _client.Dispose ();
            _client = null;
        }

        private IEnumerable<FileTransferInfo> _listFiles (string folder, string pattern, bool recursive)
        {
            _setStatus (true);
            string path = folder;
            if (!String.IsNullOrEmpty (pattern))
            {
                path = path.TrimEnd ('/') + "/" + pattern;
            }
            return new FileTransferInfo[1] { new FileTransferInfo (path, 0) };
        }

        public IEnumerable<FileTransferInfo> ListFiles ()
        {
            _setStatus (true);
            return new FileTransferInfo[1] { new FileTransferInfo (Details.UriScheme.AbsoluteUri, 0) };
        }

        public IEnumerable<FileTransferInfo> ListFiles (string folder, bool recursive)
        {
            return _listFiles (folder, null, recursive);
        }

        public IEnumerable<FileTransferInfo> ListFiles (string folder, string fileMask, bool recursive)
        {
            return _listFiles (folder, fileMask, recursive);
        }

        public StreamTransferInfo GetFileStream (string file)
        {
            _setStatus (true);
            
            // download files
            if (_client == null)
                _client = new HttpClient ();
            return new StreamTransferInfo
            {
                FileName = file,
                FileStream = _client.GetStreamAsync (file).Result
            };
        }

        public IEnumerable<StreamTransferInfo> GetFileStreams (string folder, string fileMask, bool recursive)
        {
            _setStatus (true);

            string path = folder;
            if (!String.IsNullOrEmpty (fileMask))
            {                
                path = path.TrimEnd ('/') + "/" + fileMask;
            }

            // download files
            if (_client == null)
                _client = new HttpClient ();            
            yield return GetFileStream (path);        
        }

        public IEnumerable<StreamTransferInfo> GetFileStreams ()
        {
            yield return GetFileStream (Details.UriScheme.AbsoluteUri);
        }

        public FileTransferInfo GetFile (string file, string outputDirectory, bool deleteOnSuccess)
        {
            outputDirectory = outputDirectory.Replace ('\\', '/');
            if (!outputDirectory.EndsWith ("/"))
                outputDirectory += "/";
            FileTransferHelpers.CreateDirectory (outputDirectory);

            // download files
            var f = GetFileStream (file);

            string newFile = System.IO.Path.Combine (outputDirectory, System.IO.Path.GetFileName (f.FileName));
            FileTransferHelpers.DeleteFile (newFile);

            try
            {
                using (var output = new FileStream (newFile, FileMode.Create, FileAccess.Write, FileShare.Delete | FileShare.Read, FileTransferConnectionInfo.DefaultWriteBufferSize))
                {
                    f.FileStream.CopyTo (output, FileTransferConnectionInfo.DefaultWriteBufferSize >> 2);
                }

                // check if we must remove file
                if (deleteOnSuccess)
                {
                    FileTransferHelpers.DeleteFile (f.FileName);
                }

                _setStatus (true);
            }
            catch (Exception ex)
            {
                _setStatus (ex);
                FileTransferHelpers.DeleteFile (newFile);
                newFile = null;
            }
            finally
            {
                f.FileStream.Close ();
            }

            // check if file was downloaded
            if (newFile != null)
            {
                var info = new System.IO.FileInfo (newFile);
                if (info.Exists)
                    return new FileTransferInfo (newFile, info.Length, info.CreationTime, info.LastWriteTime);
            }
            return null;
        }

        public string GetFileAsText (string file)
        {
            using (var reader = new StreamReader (GetFileStream (file).FileStream, FileTransferHelpers.TryGetEncoding (Details.Get ("encoding", "ISO-8859-1")), true))
                return reader.ReadToEnd ();
        }

        public IEnumerable<FileTransferInfo> GetFiles (string folder, string fileMask, bool recursive, string outputDirectory, bool deleteOnSuccess)
        {
            outputDirectory = outputDirectory.Replace ('\\', '/');
            if (!outputDirectory.EndsWith ("/"))
                outputDirectory += "/";
            FileTransferHelpers.CreateDirectory (outputDirectory);

            // download files
            foreach (var f in GetFileStreams (folder, fileMask, recursive))
            {
                string newFile = System.IO.Path.Combine (outputDirectory, System.IO.Path.GetFileName (f.FileName));
                FileTransferHelpers.DeleteFile (newFile);

                try
                {
                    using (var file = new FileStream (newFile, FileMode.Create, FileAccess.Write, FileShare.Delete | FileShare.Read, FileTransferConnectionInfo.DefaultWriteBufferSize))
                    {
                        f.FileStream.CopyTo (file, FileTransferConnectionInfo.DefaultWriteBufferSize >> 2);
                    }

                    // check if we must remove file
                    if (deleteOnSuccess)
                    {
                        FileTransferHelpers.DeleteFile (f.FileName);
                    }

                    _setStatus (true);
                }
                catch (Exception ex)
                {
                    _setStatus (ex);
                    FileTransferHelpers.DeleteFile (newFile);
                    newFile = null;
                }
                finally
                {
                    f.FileStream.Close ();
                }

                // check if file was downloaded
                if (newFile != null)
                {
                    var info = new System.IO.FileInfo (newFile);
                    if (info.Exists)
                        yield return new FileTransferInfo (newFile, info.Length, info.CreationTime, info.LastWriteTime);
                }
            }
        }

        public IEnumerable<FileTransferInfo> GetFiles (string outputDirectory, bool deleteOnSuccess)
        {
            return GetFiles (Details.UriScheme.AbsoluteUri, null, !Details.SearchTopDirectoryOnly, outputDirectory, deleteOnSuccess);
        }

        public bool RemoveFiles (IEnumerable<string> files)
        {            
            _setStatus (false, "Operation not supported");
            return Status;
        }

        public bool RemoveFile (string file)
        {
            _setStatus (false, "Operation not supported");
            return Status;
        }

        public bool SendFile (string localFilename)
        {
            _setStatus (false, "Operation not supported");
            return Status;
        }

        public bool SendFile (string localFilename, string destFilename)
        {
            _setStatus (false, "Operation not supported");
            return Status;
        }

        public bool SendFile (Stream localFile, string destFullPath, bool closeInputStream)
        {
            _setStatus (false, "Operation not supported");
            return Status;
        }

        public bool MoveFile (string localFilename, string destFilename)
        {
            _setStatus (false, "Operation not supported");
            return Status;
        }

        public Stream OpenWrite ()
        {
            return OpenWrite (Details.FullPath);
        }

        public Stream OpenWrite (string destFullPath)
        {
            _setStatus (false, "Operation not supported");
            return null;
        }
    }
}
