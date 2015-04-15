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
    public class TasksModule : NancyModule
    {
        public TasksModule () : base ("/Tasks")
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
                            result = new Dictionary<string,string>()
                        };
                    }                
                default:
                    return new QueryResponse (false, "Invalid parameters");
            }
            //return Response.AsText ("");
        }

    }
}
