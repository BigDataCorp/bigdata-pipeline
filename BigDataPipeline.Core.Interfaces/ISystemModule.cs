using BigDataPipeline.Interfaces;

namespace BigDataPipeline.Core.Interfaces
{
    public interface ISystemModule : IActionModule
    {
        PipelineJob GetJobRegistrationDetails ();

        void SetSystemParameters (IStorageModule storageContext);

    }
}
