using System.Collections.Generic;

namespace BigDataPipeline.Interfaces
{
    public interface IRecordCollection
    {
        IEnumerable<Record> GetStream ();
    }
}
