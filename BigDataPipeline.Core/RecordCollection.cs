using BigDataPipeline.Interfaces;
using System.Collections.Generic;

namespace BigDataPipeline.Core
{
    public class RecordCollection : IRecordCollection
    {
        IEnumerable<Record> _records;

        public RecordCollection ()
        {
        }

        public RecordCollection (IEnumerable<Record> records)
        {
            _records = records;
        }

        public void SetStream (IEnumerable<Record> records)
        {
            _records = records;
        }

        public IEnumerable<Record> GetStream ()
        {
            return _records;
        }
    }
}
