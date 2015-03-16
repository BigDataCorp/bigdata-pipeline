using BigDataPipeline.Core.Interfaces;
using BigDataPipeline.Web.Models;
using Nancy;
using Nancy.Security;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BigDataPipeline.Web
{
    public class AccessControlContext : IAccessControlMapper 
    {
        private IAccessControlModule _module;

        public AccessControlContext (IAccessControlFactory module)
        {
            _module = module.GetAccessControlModule ();
        }
        
        public IUserIdentity GetUserFromIdentifier (string identifier, NancyContext context)
        {
            var res = _module.GetUserFromIdentifier (identifier);
            return res == null || !res.HasOption ("UserName") ? null : new UserIdentityModel { UserName = res.Get ("UserName") };
        }

        public string OpenSession (string username, string password, TimeSpan? duration)
        {
            return _module.OpenSession (username, password, duration);
        }
    }
 
    public interface IAccessControlMapper
    {
        IUserIdentity GetUserFromIdentifier (string identifier, NancyContext context);

        string OpenSession (string username, string password, TimeSpan? duration);
    }
}
