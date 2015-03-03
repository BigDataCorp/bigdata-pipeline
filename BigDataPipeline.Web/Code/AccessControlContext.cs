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
        private static List<Tuple<string, string, Guid>> users = new List<Tuple<string, string, Guid>> ();

        static AccessControlContext ()
        {
            users.Add (new Tuple<string, string, Guid> ("admin", "password", new Guid ("55E1E49E-B7E8-4EEA-8459-7A906AC4D4C0")));
        }

        private IAccessControlModule _module;

        public AccessControlContext (IAccessControlFactory module)
        {
            _module = module.GetAccessControlModule ();
        }

        public IUserIdentity GetUserFromIdentifier (Guid identifier, NancyContext context)
        {
            
            var res = _module.GetUserFromIdentifier (identifier);
            return res == null || !res.HasOption ("UserName") ? null : new UserIdentityModel { UserName = res.Get ("UserName") };
        }

        public Guid? ValidateUser (string username, string password)
        {
            return _module.ValidateUser (username, password);
        }
    }
}
