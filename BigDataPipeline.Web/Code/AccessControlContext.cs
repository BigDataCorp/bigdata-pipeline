using BigDataPipeline.Core.Interfaces;
using BigDataPipeline.Web.Models;
using Nancy;
using Nancy.Authentication.Forms;
using Nancy.Security;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BigDataPipeline.Web
{
    public class AccessControlContext : IUserMapper
    {
        private IAccessControlModule _module;

        public AccessControlContext (IAccessControlFactory module)
        {
            _module = module.GetAccessControlModule ();
        }

        public IUserIdentity GetUserFromIdentifier (Guid identifier, NancyContext context)
        {            
            return GetUserFromIdentifier (identifier.ToString (), context);
        }

        public IUserIdentity GetUserFromIdentifier (string identifier, NancyContext context)
        {
            var res = _module.GetUserFromIdentifier (identifier);
            return res == null || !res.HasOption ("UserName") ? null : new UserIdentityModel { UserName = res.Get ("UserName") };
        }

        public string ValidateUser (string username, string password)
        {
            return _module.ValidateUser (username, password);
        }
    }
}
