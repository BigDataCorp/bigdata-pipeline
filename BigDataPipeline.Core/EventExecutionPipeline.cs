using BigDataPipeline.Core.Interfaces;
using BigDataPipeline.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BigDataPipeline.Core
{
    public class EventExecutionPipeline
    {        
        Dictionary<string, Dictionary<string, List<SessionContext>>> _handlers = new Dictionary<string, Dictionary<string, List<SessionContext>>> (StringComparer.Ordinal);
        Dictionary<string, List<SessionContext>> _globalHandlers = new Dictionary<string, List<SessionContext>> (StringComparer.Ordinal);
        Dictionary<string, Dictionary<string, List<SessionContext>>> _updateBuffer;        
        Dictionary<string, List<SessionContext>> _updateBufferGlobalEvents;
        IStorageModule _storage;

        static EventExecutionPipeline _instance = new EventExecutionPipeline ();
        
        public static EventExecutionPipeline Instance
        {
            get
            {
                return _instance;
            }
        }

        private EventExecutionPipeline () { }

        public void Initialize (IStorageModule storage)
        {
            _storage = storage;
        }

        public void StartUpdatePhase()
        {
            _updateBuffer = new Dictionary<string, Dictionary<string, List<SessionContext>>> (StringComparer.Ordinal);
            _updateBufferGlobalEvents = new Dictionary<string, List<SessionContext>> (StringComparer.Ordinal);
        }

        public void RegisterHandlers (PipelineJob job)
        {
            Dictionary<string, List<SessionContext>> localEvents = null;

            // search collection jobs looking for event handlers
            if (job != null)
            {
                List<SessionContext> list;
                Dictionary<string, List<SessionContext>> map;

                if (job.Events != null)
                {
                    foreach (var e in job.Events)
                    {
                        var key = prepareEventKey (e);

                        // select event type list
                        if (isLocalEvent (e))
                        {
                            if (localEvents == null)
                                localEvents = new Dictionary<string, List<SessionContext>> (StringComparer.Ordinal);
                            map = localEvents;
                        }
                        else
                        {
                            map = _updateBufferGlobalEvents;
                        }

                        // get list
                        if (!map.TryGetValue (key, out list))
                        {
                            list = new List<SessionContext> ();
                            map.Add (key, list);
                        }

                        // register event handler
                        list.Add (new SessionContext
                        {
                            Id = job.Id,
                            Job = job,
                            Origin = TaskOrigin.EventHandler
                        });
                    }
                }
            }

            // update event list
            if (localEvents != null)
            {
                _updateBuffer[job.Group ?? ""] = localEvents;                
            }
        }

        public void EndUpdatePhase()
        {
            // switch handler lists
            _handlers = _updateBuffer;
            _globalHandlers = _updateBufferGlobalEvents;
            // cleanup
            _updateBuffer = null;
            _updateBufferGlobalEvents = null;
        }

        private bool isLocalEvent (string eventName)
        {
            // TODO: change this test to check if the value before '.' is a valid domain
            return eventName.StartsWith ("local.", StringComparison.Ordinal) || eventName.StartsWith ("this.", StringComparison.Ordinal);
        }

        private string prepareEventKey (string eventName)
        {
            if (isLocalEvent (eventName))
                return eventName.Substring (eventName.IndexOf ('.') + 1);
            return eventName;
        }

        public void FireEvent (string eventName, Record eventData, PipelineJob currentJob)
        {
            List<SessionContext> list = null;
            var key = prepareEventKey (eventName);
            // get registered event handlers
            if (isLocalEvent (eventName))
            {
                if (currentJob != null)
                {
                    Dictionary<string, List<SessionContext>> evts;
                    if (_handlers.TryGetValue (currentJob.Group ?? "", out evts))
                        evts.TryGetValue (key, out list);
                }
            }
            else
            {
                _globalHandlers.TryGetValue (key, out list);
            }

            // execute actions
            if (list != null)
            {
                foreach (var i in list)
                {
                    // load pipeline
                    var job = _storage.GetPipelineCollection (i.Job.Id);
                    if (job == null || !job.Enabled || job.RootAction == null)
                        continue;
                    var ctx = new SessionContext
                    {
                        Job = job,
                        Origin = i.Origin,
                        Start = DateTime.UtcNow
                    };
                    // TODO: this event is bronken
                    ctx.Emit (eventData);
                    ctx.FinalizeAction ();
                    TaskExecutionPipeline.Instance.TryAddTask (ctx);
                }
            }
        }
    }
}
