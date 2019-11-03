using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HSD;

namespace RDHasher.CollisionChecker
{
    public class DoubleHashedDataSetWrapper : IDataSet
    {
        public HSD.DoubleHashedDataSet<string, string> DataSet { get; protected set; }

        public long Count => DataSet.Count;
        public int MaximumQueueLength => DataSet.MaximumQueueLength;

        public DoubleHashedDataSetWrapper(long iterations, bool compress)
        {
            DataSet = new DoubleHashedDataSet<string, string>(iterations, compress);
        }

        public long Add(HSD.Tuple<string, string> item)
        {
            return DataSet.Add(item);
        }

        public bool ContainsKey(string key)
        {
            return DataSet.ContainsKey(key);
        }

        public bool ContainsKey(string key, out HSD.Tuple<string, string> item, out long address)
        {
            return DataSet.ContainsKey(key, out item, out address);
        }

        public bool ContainsValue(string value)
        {
            return DataSet.ContainsValue(value);
        }

        public bool ContainsValue(string value, out HSD.Tuple<string, string> item, out long address)
        {
            return DataSet.ContainsValue(value, out item, out address);
        }
        public void Clear()
        {
            DataSet.Clear();
        }
    }
}
