using BigDataPipeline.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BigDataPipeline.Core
{
    public class FileSystemTransfer : IFileTransfer
    {
        static string[] serviceSchemes = new[] { "file", "" };
        
        /// <summary>
        /// Gets the service prefix.
        /// http://en.wikipedia.org/wiki/File_URI_scheme
        /// c:/[path]/[file name or wildcard expression]
        /// file://[host]/[path]/[file name or wildcard expression]        
        /// </summary>
        /// <example>
        /// Windows:
        /// file:///c:/test/test.txt
        /// file://localhost/c:/test/test.txt
        /// Linux:
        /// file://localhost/etc/test.txt
        /// file:///etc/test.txt
        /// </example>
        /// <returns></returns>
        public IEnumerable<string> GetSchemeNames ()
        {
            return serviceSchemes;
        }

        public FileServiceConnectionInfo ParseConnectionUri (string connectionUri, IEnumerable<KeyValuePair<string, string>> extraOptions)
        {
            return new FileServiceConnectionInfo (connectionUri, extraOptions);
        }

        public FileServiceConnectionInfo Details { get; private set; }

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

        public bool Open (FileServiceConnectionInfo details)
        {
            Details = details;
            if (Details.RetryCount <= 0)
                Details.RetryCount = 1;
            return true;
        }        

        private void CreateDirectory (string folder)
        {
            if (String.IsNullOrEmpty (folder))
                return;
            FileTransferHelpers.CreateDirectory (folder);
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
        }

        private IEnumerable<FileTransferInfo> _listFiles (string folder, string pattern, bool recursive)
        {
            _setStatus (true);

            folder = PreparePath (folder);

            if (String.IsNullOrEmpty (pattern))
                pattern = "*";
            
            if (!System.IO.Directory.Exists (folder))
                return new FileTransferInfo[0];

            return System.IO.Directory.EnumerateFiles (folder, pattern, recursive ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly)
                       .Select (i => new FileInfo (i)).Select (i => new FileTransferInfo (i.FullName, i.Length, i.CreationTime, i.LastWriteTime));
        }

        public IEnumerable<FileTransferInfo> ListFiles ()
        {
            return ListFiles (Details.BasePath, Details.SearchPattern, !Details.SearchTopDirectoryOnly);
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
            return new StreamTransferInfo
            {
                FileName = file,
                FileStream = new FileStream (file, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read, FileServiceConnectionInfo.DefaultReadBufferSize)
            };
        }

        public IEnumerable<StreamTransferInfo> GetFileStreams (string folder, string fileMask, bool recursive)
        {
            _setStatus (true);
            // download files
            foreach (var f in _listFiles (folder, fileMask, recursive))
            {
                yield return new StreamTransferInfo
                {
                    FileName = f.FileName,
                    FileStream = new FileStream (f.FileName, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read, FileServiceConnectionInfo.DefaultReadBufferSize)
                };
            }
        }

        public IEnumerable<StreamTransferInfo> GetFileStreams ()
        {
            _setStatus (true);
            // download files
            foreach (var f in ListFiles ())
            {
                yield return new StreamTransferInfo
                {
                    FileName = f.FileName,
                    FileStream = new FileStream (f.FileName, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read, FileServiceConnectionInfo.DefaultReadBufferSize)
                };
            }
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
                using (var output = new FileStream (newFile, FileMode.Create, FileAccess.Write, FileShare.Delete | FileShare.Read, FileServiceConnectionInfo.DefaultWriteBufferSize))
                {
                    f.FileStream.CopyTo (output, FileServiceConnectionInfo.DefaultWriteBufferSize >> 2);
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
                    using (var file = new FileStream (newFile, FileMode.Create, FileAccess.Write, FileShare.Delete | FileShare.Read, FileServiceConnectionInfo.DefaultWriteBufferSize))
                    {
                        f.FileStream.CopyTo (file, FileServiceConnectionInfo.DefaultWriteBufferSize >> 2);
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
            return GetFiles (Details.BasePath, Details.SearchPattern, !Details.SearchTopDirectoryOnly, outputDirectory, deleteOnSuccess);
        }

        public bool RemoveFiles (IEnumerable<string> files)
        {
            foreach (var f in files)
                FileTransferHelpers.DeleteFile (f);

            _setStatus (true);

            return Status;
        }

        public bool RemoveFile (string file)
        {
            FileTransferHelpers.DeleteFile (file);
            _setStatus (true);
            return Status;
        }

        public bool SendFile (string localFilename)
        {
            return SendFile (localFilename, System.IO.Path.Combine (Details.BasePath, System.IO.Path.GetFileName (localFilename)));
        }

        public bool SendFile (string localFilename, string destFilename)
        {
            using (var file = new FileStream (localFilename, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read, 1024 * 1024))
            {
                return SendFile (file, destFilename, false);
            }
        }

        public bool SendFile (Stream localFile, string destFullPath, bool closeInputStream)
        {
            try
            {
                FileTransferHelpers.CreateDirectory (System.IO.Path.GetDirectoryName (destFullPath));

                using (var file = new FileStream (destFullPath, FileMode.Create, FileAccess.Write, FileShare.Delete | FileShare.Read, FileServiceConnectionInfo.DefaultWriteBufferSize))
                {
                    localFile.CopyTo (file, FileServiceConnectionInfo.DefaultWriteBufferSize >> 2);
                }
                _setStatus (true);
            }
            catch (Exception ex)
            {
                _setStatus (ex);
            }
            finally
            {
                if (closeInputStream)
                    localFile.Close ();
            }
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
            FileTransferHelpers.CreateDirectory (System.IO.Path.GetDirectoryName (destFullPath));
            // upload
            return new FileStream (destFullPath, FileMode.Create, FileAccess.Write, FileShare.Delete | FileShare.Read, FileServiceConnectionInfo.DefaultWriteBufferSize);
        }
    }
}
