using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RDHasher.CollisionChecker
{
    public interface IDataSet
    {
        long Count { get; }
        int MaximumQueueLength { get; }
        bool ContainsKey(string key);
        bool ContainsValue(string value);
        bool ContainsKey(string key, out HSD.Tuple<string, string> item, out long address);
        bool ContainsValue(string value, out HSD.Tuple<string, string> item, out long address);
        long Add(HSD.Tuple<string, string> item);
        void Clear();
    }
}
