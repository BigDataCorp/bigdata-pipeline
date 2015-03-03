using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BigDataPipeline.Web.Models
{
    public class QueryResponse<T>
    {
        public bool status { get; set; }
        public string message { get; set; }
        public T result { get; set; }
        public QueryResponse ()
        {
        }
        public QueryResponse (bool status, string message = "")
        {
            this.status = status;
            this.message = message;
        }
    }

    public class QueryResponse : QueryResponse<object>
    {
        public QueryResponse ()
        {
        }
        public QueryResponse (bool status, string message = "")
        {
            this.status = status;
            this.message = message;
        }
    }
}
