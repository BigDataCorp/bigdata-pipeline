using Nancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestNancyModule
{
    public class TestNancyModule : NancyModule
    {
        static long counter = 0;

        public TestNancyModule () : base ("/TestNancy")
        {
            Get["/"] = _ => View["Index"];
            Get["/test"] = _ => View["Test"];
            Get["/counter"] = p => "Counter = " + System.Threading.Interlocked.Increment(ref counter).ToString ();
            Get["/obj"] = p => new MyObj{ Counter = System.Threading.Interlocked.Increment (ref counter), Id = Guid.NewGuid () };
        }
    }

    [Serializable]
    public class MyObj
    {
        public long Counter { get; set; }
        public Guid Id { get; set; }
    }
}
