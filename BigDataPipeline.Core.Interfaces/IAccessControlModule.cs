using BigDataPipeline.Interfaces;
using System;

namespace BigDataPipeline.Core.Interfaces
{
    public interface IAccessControlModule
    {
        void Initialize (Record systemOptions);

        FlexibleObject GetUserFromIdentifier (Guid identifier);

        Guid? ValidateUser (string username, string password);
    }
}
