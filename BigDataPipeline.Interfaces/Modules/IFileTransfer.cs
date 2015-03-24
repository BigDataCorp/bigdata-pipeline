using System;
using System.Collections.Generic;
using System.IO;

namespace BigDataPipeline.Interfaces
{    

    public interface IFileTransfer : IDisposable
    {
        /// <summary>
        /// Gets the URI scheme names that this instance can handle.
        /// </summary>
        IEnumerable<string> GetSchemeNames ();

        /// <summary>
        /// Implements the parser of the connection URI
        /// </summary>
        /// <param name="connectionUri"></param>
        /// <param name="extraOptions">
        /// Custom options for this service:<para/>
        /// retryCount (all), retryWaitMs (all), searchTopDirectoryOnly (some), sshKeyFiles (SFTP), useReducedRedundancy (S3), makePublic (S3), partSize (S3)
        /// </param>
        FileServiceConnectionInfo ParseConnectionUri (string connectionUri, IEnumerable<KeyValuePair<string, string>> extraOptions);

        string LastError { get; }

        FileServiceConnectionInfo Details { get; }

        bool Status { get; }

        /// <summary>
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
        /// 
        /// </summary>
        bool Open (FileServiceConnectionInfo details);
        
        bool IsOpened ();

        IEnumerable<FileTransferInfo> ListFiles ();

        IEnumerable<FileTransferInfo> ListFiles (string folder, bool recursive);

        IEnumerable<FileTransferInfo> ListFiles (string folder, string fileMask, bool recursive);

        StreamTransferInfo GetFileStream (string file);

        IEnumerable<StreamTransferInfo> GetFileStreams (string folder, string fileMask, bool recursive);

        IEnumerable<StreamTransferInfo> GetFileStreams ();

        FileTransferInfo GetFile (string file, string outputDirectory, bool deleteOnSuccess);

        string GetFileAsText (string file);

        IEnumerable<FileTransferInfo> GetFiles (string folder, string fileMask, bool recursive, string outputDirectory, bool deleteOnSuccess);

        IEnumerable<FileTransferInfo> GetFiles (string outputDirectory, bool deleteOnSuccess);

        bool RemoveFiles (IEnumerable<string> files);

        bool RemoveFile (string file);

        bool SendFile (string localFilename);

        bool SendFile (string localFilename, string destFilename);

        bool SendFile (Stream localFile, string destFullPath, bool closeInputStream);

        bool MoveFile (string localFilename, string destFilename);

        Stream OpenWrite ();

        Stream OpenWrite (string destFullPath);
    }
}