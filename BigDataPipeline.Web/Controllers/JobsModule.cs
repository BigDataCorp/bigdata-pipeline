using BigDataPipeline.Core;
using BigDataPipeline.Web.Models;
using Nancy;
using Nancy.Extensions;
using Nancy.Authentication.Forms;
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
    public class JobsModule : NancyModule
    {
        public JobsModule () : base ("/Jobs")
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
                            result = PipelineService.Instance.GetStorage ().GetPipelineCollections (false)
                        };
                    }
                case "save":
                    {
                        PipelineJob job;
                        try
                        {
                            job = item.GetData<PipelineJob> ("job");
                        }
                        catch (Exception e)
                        {
                            return new QueryResponse (false, "Error parsing Job: " + e.Message);
                        }
                        if (String.IsNullOrWhiteSpace (job.Id))
                            return new QueryResponse (false, "Invalid Job Id");
                        if (String.IsNullOrWhiteSpace (job.Name))
                            return new QueryResponse (false, "Invalid Job Name");
                        PipelineService.Instance.GetStorage ().SavePipelineCollection (job);
                        return new QueryResponse (true);
                    }
                case "remove":
                    {
                        PipelineService.Instance.GetStorage ().RemovePipelineCollection (item.GetData ("jobId", ""));                        
                        return new QueryResponse (true);
                    }
                case "play":
                    {
                        var job = PipelineService.Instance.GetStorage ().GetPipelineCollection (item.GetData ("jobId", ""));
                        if (job == null)
                            return new QueryResponse (false, "Collection not found!");

                        var task = new SessionContext
                        {
                            Id = job.Id,
                            Job = job,
                            Start = DateTime.UtcNow,
                            Origin = TaskOrigin.Scheduller
                        };
                        task.Options.Set ("actionLogLevel", "Trace");

                        bool result = TaskExecutionPipeline.Instance.TryAddTask (task);

                        return new QueryResponse (result, result ? "" : "Job ");
                    }
                default:
                    return new QueryResponse (false, "Invalid parameters");
            }
            //return Response.AsText ("");
        }

    }
}
