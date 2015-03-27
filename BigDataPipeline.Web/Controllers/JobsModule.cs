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
                            result = PipelineService.Instance.GetStorage ().GetPipelineJobs (false)
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
                        if (String.IsNullOrWhiteSpace (job.Name))
                            return new QueryResponse (false, "Invalid Job Name");
                        // load current job
                        var currentJob = PipelineService.Instance.GetStorage ().GetPipelineJob (job.Id);
                        // validate scheduler
                        if (job.Scheduler != null && job.Scheduler.Count > 0)
                            job.SetScheduler (job.Scheduler.ToArray ());
                        // keep old execution date
                        if (currentJob != null)
                            job.LastExecution = currentJob.LastExecution;

                        // recalculate stale next execution
                        if (job.NextExecution < DateTime.UtcNow.Subtract (PipelineJob.SchedulerLowThreshold))
                        {
                            if (currentJob != null && job.NextExecution < currentJob.NextExecution)                                
                                job.NextExecution = currentJob.NextExecution;
                            else
                                job.RecalculateScheduler ();
                        }
                        // adjust events
                        if (job.Events != null && job.Events.Count > 0)
                            job.Events = new HashSet<string> (job.Events.Where (i => !String.IsNullOrWhiteSpace (i)).Select (i => i.Trim ()));
                        PipelineService.Instance.GetStorage ().SavePipelineJob (job);
                        return new QueryResponse (true);
                    }
                case "remove":
                    {
                        PipelineService.Instance.GetStorage ().RemovePipelineJob (item.GetData ("jobId", ""));                        
                        return new QueryResponse (true);
                    }
                case "play":
                    {
                        var job = PipelineService.Instance.GetStorage ().GetPipelineJob (item.GetData ("jobId", ""));
                        if (job == null)
                            return new QueryResponse (false, "Job not found!");

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
