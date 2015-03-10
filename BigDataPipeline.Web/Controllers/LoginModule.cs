using Nancy;
using Nancy.ModelBinding;
using System;
using Nancy.Authentication.Forms;
using BigDataPipeline.Web.Models;
using BigDataPipeline.Core.Interfaces;

namespace BigDataPipeline.Web.site.Controllers
{
    public class LoginModule : NancyModule
    {
        IAccessControlFactory _module;

        public LoginModule (IAccessControlFactory module)
        {
            _module = module;

            Get["/logout"] = _ => this.LogoutAndRedirect ("/login");
            Post["/logout"] = _ => this.LogoutWithoutRedirect ();
            
            Get["/login"] = GetLoginPage;
            Post["/login"] = _ => ValidateLogin ();
        }

        private dynamic GetLoginPage (dynamic p)
        {
            return View["Login"];
        }

        private dynamic ValidateLogin ()
        {
            // Binding Request body to my class
            var loginModel = this.Bind<QueryRequest>();

            // Reading values from the model
            string username = loginModel.GetData("Username", "");
            string password = loginModel.GetData("Password", "");

            // Validation Logic
            var ac = _module.GetAccessControlModule ();
            if (ac == null)
                return Response.AsJson (new QueryResponse (true));

            var session = ac.ValidateUser (username, password);
            if (!String.IsNullOrEmpty (session))
            {
                var response = Response.AsJson (new QueryResponse (true));

                // prepare login cookie authentication
                Guid guid;
                if (Guid.TryParse (session, out guid))
                {
                    var authResponse = this.LoginWithoutRedirect (guid, DateTime.UtcNow.AddDays (14));                
                    foreach (var c in authResponse.Cookies)
                        response.Cookies.Add (c);
                }
                return response;
            }

            return Response.AsJson(new QueryResponse (false, "Incorrect login or password."));
        }
    }

    

    
}
