using BigDataPipeline.Interfaces;
using System;
using System.Collections.Generic;

namespace BigDataPipeline.Core.Interfaces
{
    public interface IStorageModule : IDisposable
    {
        IEnumerable<ModuleParameterDetails> GetParameterDetails ();

        void Initialize (Record systemOptions);

        IEnumerable<PipelineJob> GetPipelineJobs (bool filterDisabledJobs = true);

        PipelineJob GetPipelineJob (string itemId);

        bool SavePipelineJob (PipelineJob item);

        bool RemovePipelineJob (string itemId);

        Dictionary<string, string> GetConfigValues ();

        string GetConfigValue (string key);

        bool SaveConfigValue (string key, string value);
        
        IEnumerable<ISessionContext> GetEnqueuedTasks ();

        bool EnqueueTask (ISessionContext task);

        bool RemoveEnqueuedTask (ISessionContext task);
    }
}