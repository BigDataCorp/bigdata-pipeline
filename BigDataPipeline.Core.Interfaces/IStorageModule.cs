using BigDataPipeline.Interfaces;
using System;
using System.Collections.Generic;

namespace BigDataPipeline.Core.Interfaces
{
    public interface IStorageModule : IDisposable
    {
        IEnumerable<PluginParameterDetails> GetParameterDetails ();

        void Initialize (Record systemOptions);

        IEnumerable<PipelineJob> GetPipelineCollections (bool filterDisabledJobs = true);

        PipelineJob GetPipelineCollection (string itemId);

        bool SavePipelineCollection (PipelineJob item);

        bool RemovePipelineCollection (string itemId);

        Dictionary<string, string> GetConfigValues ();

        string GetConfigValue (string key);

        bool SaveConfigValue (string key, string value);
        
        IEnumerable<ISessionContext> GetEnqueuedTasks ();

        bool EnqueueTask (ISessionContext task);

        bool RemoveEnqueuedTask (ISessionContext task);
    }
}