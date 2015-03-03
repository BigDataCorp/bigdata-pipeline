using Nancy;
using Nancy.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BigDataPipeline.Web.site.Controllers
{
    public class HomeModule : NancyModule
    {
        public HomeModule () : base ("/")
        {
            this.RequiresAuthentication ();

            Get["/"] = Index;

            Get["/complex"] = r => "this is a post only operation ;-)";

            Post["/complex"] = r => new { Name = "Index", Description = "oi oi oi", Count = 10000000 };
        }

        private dynamic Index (dynamic parameters)
        {
            return View["Index"];
        }

    }
}
