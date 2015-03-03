using BigDataPipeline.Core;
using BigDataPipeline.Web.Models;
using Nancy;
using Nancy.ModelBinding;
using Nancy.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BigDataPipeline.Web.site.Controllers
{
    public class EventsModule : NancyModule
    {
        public EventsModule () : base ("/Events")
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
                            result = PipelineService.Instance.GetActionLoggerStorage ().Read (null, null, null, null, null, 500, null, true)
                        };
                    }
                    break;
                default:
                    return new QueryResponse (false, "Invalid parameters");
            }

            //return Response.AsText ("");
        }

    }
}
