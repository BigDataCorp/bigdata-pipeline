using BigDataPipeline.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BigDataPipeline.DummyAccessControlModule
{
    public class DummyAccessControl : BigDataPipeline.Core.Interfaces.IAccessControlModule
    {
        private static List<DummyUser> users = new List<DummyUser> ();

        public void Initialize (Record systemOptions)
        {
            users.Add (systemOptions.Get ("DummyAccessControlUser", new DummyUser
            {
                UserName = "admin",
                Password = "password",
                Guid = new Guid ("55E1E49E-B7E8-4EEA-8459-7A906AC4D4C0")
            }));
        }

        public FlexibleObject GetUserFromIdentifier (Guid identifier)
        {
            var userRecord = users.Where (u => u.Guid == identifier).FirstOrDefault ();
            return userRecord == null ? null : new FlexibleObject ().Set ("UserName", userRecord.UserName);
        }

        public Guid? ValidateUser (string username, string password)
        {
            var userRecord = users.Where (u => u.UserName.Equals (username, StringComparison.OrdinalIgnoreCase) && u.Password == password).FirstOrDefault ();

            if (userRecord == null)
                return null;

            return userRecord.Guid;
        }
    }

    public class DummyUser
    {
        public string UserName { get; set;  }
        public string Password { get; set; }
        public Guid Guid { get; set; }
    }
}
