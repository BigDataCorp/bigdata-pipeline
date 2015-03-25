using BigDataPipeline.Core;
using BigDataPipeline.Web.Models;
using Nancy;
using Nancy.Extensions;
using Nancy.Security;
using Nancy.ModelBinding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BigDataPipeline.Interfaces;

namespace BigDataPipeline.Web.site.Controllers
{
    public class ModulesModule : NancyModule
    {
        public ModulesModule () : base ("/Modules")
        {
            this.RequiresAuthentication ();

            Get["/"] = p => View["Index"];

            Post["/query"] = QueryEvents;
        }

        private dynamic QueryEvents (dynamic p)
        {
            var item = this.Bind<QueryRequest> ();

            if (item == null || item.op == null)
                return new QueryResponse (false, "Invalid parameters");

            switch (item.op.ToLowerInvariant ())
            {
                case "load":
                    {                        
                        return new QueryResponse (true)
                        {
                            result = BigDataPipeline.Core.ModuleContainer.Instance.GetTypesOf<IActionModule> ()
                                         .Select (i => BigDataPipeline.Core.ModuleContainer.Instance.GetInstanceAs<IActionModule> (i))
                                         .Where (i => i != null)
                                         .Select (i => new
                                         {
                                             Name = i.GetType ().Name,
                                             FullName = i.GetType ().FullName,
                                             Description = i.GetDescription (),
                                             Parameters = i.GetParameterDetails ()
                                         })
                        };
                    }                
                default:
                    return new QueryResponse (false, "Invalid parameters");
            }
            //return Response.AsText ("");
        }

    }
}
