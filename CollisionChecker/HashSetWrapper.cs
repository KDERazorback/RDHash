using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HSD;

namespace RDHasher.CollisionChecker
{
    public class HashSetWrapper : IDataSet
    {
        public Hashtable HashSet { get; protected set; }
        public Hashtable HashSetInverted { get; protected set; }

        public long Count => HashSet.Count;
        public int MaximumQueueLength => 0;

        public HashSetWrapper(long iterations)
        {
            HashSet = new Hashtable((int)iterations);
            HashSetInverted = new Hashtable((int)iterations);
        }

        public long Add(HSD.Tuple<string, string> item)
        {
            long address;
            lock (HashSet)
            {
                HashSet.Add(item.Key, item.Value);
                HashSetInverted.Add(item.Value, item.Key);
                address = HashSet.Count;
            }

            return address;
        }

        public bool ContainsKey(string key)
        {
            return HashSet.ContainsKey(key);
        }

        public bool ContainsKey(string key, out HSD.Tuple<string, string> item, out long address)
        {
            if (HashSet.ContainsKey(key))
            {
                HSD.Tuple<string, string> tuple = new HSD.Tuple<string, string>(key, null);
                tuple.Value = (string)HashSet[key];
                address = 0;
                item = tuple;

                return true;
            }
            else
            {
                item = null;
                address = 0;
                return false;
            }
        }

        public bool ContainsValue(string value)
        {
            return HashSetInverted.ContainsKey(value);
        }

        public bool ContainsValue(string value, out HSD.Tuple<string, string> item, out long address)
        {
            if (HashSetInverted.ContainsKey(value))
            {
                HSD.Tuple<string, string> tuple = new HSD.Tuple<string, string>(null, value);
                tuple.Value = (string)HashSetInverted[value];
                address = 0;
                item = tuple;

                return true;
            }
            else
            {
                item = null;
                address = 0;
                return false;
            }
        }

        public void Clear()
        {
            HashSet.Clear();
            HashSetInverted.Clear();
        }
    }
}
