using BigDataPipeline.Interfaces;
using System;

namespace BigDataPipeline.Core.Interfaces
{
    public interface IAccessControlModule
    {
        void Initialize (Record systemOptions);

        FlexibleObject GetUserFromIdentifier (string identifier);

        string OpenSession (string username, string password, TimeSpan? duration);
    }
}
