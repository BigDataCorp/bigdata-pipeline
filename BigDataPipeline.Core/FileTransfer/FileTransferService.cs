using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using BigDataPipeline.Interfaces;

namespace BigDataPipeline.Core
{
    public class FileTransferService : IFileTransferService
    {
        Dictionary<string, Type> _fileTransfers = null;

        public void Initialize ()
        {
            // check if the service is already initialized
            if (_fileTransfers != null)
                return;

            // try to list all file transfer services
            var map = new Dictionary<string, Type> (StringComparer.OrdinalIgnoreCase);
            // register implementations
            foreach (var f in ModuleContainer.Instance.GetInstancesOf<IFileTransfer> ())
            {
                foreach (var scheme in f.GetSchemeNames ())
                    map[scheme] = f.GetType ();
            }

            // set map
            _fileTransfers = map;            
        }

        public IFileTransfer Open (string connectionUri, FlexibleObject extraOptions = null)
        {
            Initialize ();

            // parse connectionUri
            var prefix = ExtractUriSchemeName (connectionUri);

            // locate service
            Type serviceType;
            if (_fileTransfers.TryGetValue (prefix, out serviceType))
            {
                var instance = ModuleContainer.Instance.GetInstance (serviceType) as IFileTransfer;
                instance.Open (instance.ParseConnectionUri (connectionUri, extraOptions.Options));
                return instance;
            }
            return null;                
        }
        
        private static string ExtractUriSchemeName (string input)
        {
            string[] path = new string[2];
            // try to find the uri scheme name
            var ix = input.IndexOf ("://", StringComparison.Ordinal);
            if (ix > 0)
            {
                return input.Substring (0, ix).ToLowerInvariant ();
            }
            else
            {
                // fallback to Uri implementation
                var uri = new Uri (input);
                return uri.Scheme;
            }
        }

    }

}