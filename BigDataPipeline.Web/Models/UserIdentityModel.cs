using Nancy.Security;
using System.Collections.Generic;

namespace BigDataPipeline.Web.Models
{
    public class UserIdentityModel : IUserIdentity
    {
        public IEnumerable<string> Claims { get; set; }

        public string UserName { get; set; }
    }
}
