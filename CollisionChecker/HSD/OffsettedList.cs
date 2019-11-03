using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSD
{
    public class OffsettedList<T>
    {
        protected List<T> Records { get; set; }
        public long Offset { get; protected set; }

        public int Count => Records.Count;
        public int Capacity => Records.Capacity;

        public OffsettedList(long start, int size)
        {
            Records = new List<T>(size);
            Offset = start;
        }

        public T this[long index]
        {
            get
            {
                return this[index, true];
            }
            set
            {
                lock (Records)
                    this[index, true] = value;
            }
        }
        public T this[long index, bool offsetted]
        {
            get
            {
                return Records[(int)(index - (offsetted ? Offset : 0))];
            }
            set
            {
                lock (Records)
                    Records[(int)(index - (offsetted ? Offset : 0))] = value;
            }
        }

        public long Add(T item)
        {
            long address;
            lock (Records)
            {
                Records.Add(item);
                address = Records.Count;
            }

            address += Offset;

            return address;
        }

        public void AddRange(IEnumerable<T> items)
        {
            lock (Records)
            {
                Records.AddRange(items);
            }
        }

        public void Remove(T item)
        {
            lock (Records)
                Records.Remove(item);
        }

        public void RemoveAt(long index)
        {
            RemoveAt(index, true);
        }

        public void RemoveAt(long index, bool offsetted)
        {
            if (offsetted)
                index -= Offset;

            lock (Records)
                Records.RemoveAt((int)index);
        }

        public void Clear()
        {
            Records.Clear();
        }

        public long PhysicalToVirtual(int index)
        {
            return index + Offset;
        }

        public int VirtualToPhysical(long index)
        {
            return (int)(index - Offset);
        }

        public override string ToString()
        {
            return string.Format("OffsettedList<{0}>, {1} - {2}, {3} items.", typeof(T).Name, Offset.ToString("N0"), (Offset + Capacity).ToString("N0"), Count.ToString("N0"));
        }
    }
}
