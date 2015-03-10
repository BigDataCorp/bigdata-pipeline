using BigDataPipeline.Interfaces;
using System;

namespace BigDataPipeline.Core.Interfaces
{
    public interface IAccessControlModule
    {
        void Initialize (Record systemOptions);

        FlexibleObject GetUserFromIdentifier (string identifier);

        string ValidateUser (string username, string password);
    }
}
