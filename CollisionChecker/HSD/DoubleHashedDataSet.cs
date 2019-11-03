using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HSD
{
    /// <summary>
    /// Hi-Speed Double-Hashed data set implementation with massive record count support
    /// </summary>
    public class DoubleHashedDataSet<T, Y>
    {
        public static int SegmentationBucketSize = 2000000;

        protected List<OffsettedList<Tuple<T, Y>>> DataBuckets;
        protected List<long>[] KeyMap;
        protected List<long>[] ValueMap;
        ReaderWriterLockSlim LockSlim = new ReaderWriterLockSlim();
        protected int BucketSize { get; set; } = SegmentationBucketSize;
        protected bool CompressHashCodes { get; set; } = true;
        protected int HashCodeCompressionFactor { get; set; } = 600;
        public bool PreferKeyMapAddresses { get; set; } = true;


        public long Capacity
        {
            get
            {
                LockSlim.EnterReadLock();
                long total = 0;
                for (int i = 0; i < DataBuckets.Count; i++)
                    total += DataBuckets[i].Capacity;

                LockSlim.ExitReadLock();
                return total;
            }
        }

        public long Count
        {
            get
            {
                LockSlim.EnterReadLock();
                long total = 0;
                for (int i = 0; i < DataBuckets.Count; i++)
                    total += DataBuckets[i].Count;

                LockSlim.ExitReadLock();
                return total;
            }
        }
        public int MaximumQueueLength { get; protected set; } = 0;

        protected virtual int BucketForAddress(long address)
        {
            return (int)(address / BucketSize);
        }

        public DoubleHashedDataSet(long capacity, bool compressMaps)
        {
            int hashCodeArraySize = int.MaxValue;
            if (compressMaps)
            {
                if (capacity < 1100000)
                    HashCodeCompressionFactor = 36000;
                else if (capacity > 20000000)
                    HashCodeCompressionFactor = 189;
                else
                    HashCodeCompressionFactor = (int)((6000.0f * Math.Pow((capacity / 1000.0f) - 1000, -1)) * 600);

                hashCodeArraySize = (hashCodeArraySize / HashCodeCompressionFactor) + 1;
            }
            DataBuckets = new List<OffsettedList<Tuple<T, Y>>>(1024);
            KeyMap = new List<long>[hashCodeArraySize];
            ValueMap = new List<long>[hashCodeArraySize];

            DataBuckets.Add(new OffsettedList<Tuple<T, Y>>(0, BucketSize));

            for (int i = 0; i < KeyMap.Length; i++)
            {
                KeyMap[i] = new List<long>(2);
                ValueMap[i] = new List<long>(2);
            }

            CompressHashCodes = compressMaps;
        }

        public DoubleHashedDataSet(long capacity) : this(capacity, true) { }

        public Tuple<T, Y> this[long index]
        {
            get 
            {
                LockSlim.EnterReadLock();
                int bucket = BucketForAddress(index);
                Tuple<T, Y> item = DataBuckets[bucket][index];
                LockSlim.ExitReadLock();

                return item;
            }
            set
            {
                LockSlim.EnterWriteLock();
                int bucket = BucketForAddress(index);
                DataBuckets[bucket][index] = value;
                LockSlim.ExitWriteLock();
            }
        }

        public long Add(Tuple<T, Y> item)
        {
            bool release = true;
            if (!LockSlim.IsWriteLockHeld)
                LockSlim.EnterWriteLock();
            else
                release = false;

            long address;
            OffsettedList<Tuple<T, Y>> list = DataBuckets[DataBuckets.Count - 1];
            if (list.Count >= BucketSize)
            {
                long total = 0;
                for (int i = 0; i < DataBuckets.Count; i++)
                    total += DataBuckets[i].Count;
                DataBuckets.Add(new OffsettedList<Tuple<T, Y>>(total, BucketSize));
                list = DataBuckets[DataBuckets.Count - 1];
            }

            address = list.Add(item);
            int keyhashcode = GetHashCodeForType(item.Key);
            int valuehashcode = GetHashCodeForType(item.Value);
            KeyMap[keyhashcode].Add(address);
            ValueMap[valuehashcode].Add(address);

            if (KeyMap[keyhashcode].Count > MaximumQueueLength) MaximumQueueLength = KeyMap[keyhashcode].Count;
            if (ValueMap[valuehashcode].Count > MaximumQueueLength) MaximumQueueLength = ValueMap[valuehashcode].Count;

            if (release)
                LockSlim.ExitWriteLock();

            return address;
        }

        public void AddRange(IEnumerable<Tuple<T, Y>> items)
        {
            LockSlim.EnterWriteLock();

            foreach (Tuple<T, Y> item in items)
                Add(item);

            LockSlim.ExitWriteLock();
        }

        public void Remove(Tuple<T, Y> item)
        {
            bool release = true;
            bool itemNotFound = true;
            if (!LockSlim.IsWriteLockHeld)
                LockSlim.EnterWriteLock();
            else
                release = false;

            if (ContainsItem(item, out long address))
            {
                int bucket = BucketForAddress(address);
                DataBuckets[bucket].RemoveAt(address);

                int keymapAddress = GetHashCodeForType(item.Key);
                int valuemapAddress = GetHashCodeForType(item.Value);

                KeyMap[keymapAddress].Remove(address);
                ValueMap[valuemapAddress].Remove(address);

                itemNotFound = false;
            }

            if (release)
                LockSlim.ExitWriteLock();

            if (itemNotFound)
                throw new KeyNotFoundException("The specified item cannot be found in the collection.");
        }

        public void RemoveAt(long index)
        {
            bool release = true;
            if (!LockSlim.IsWriteLockHeld)
                LockSlim.EnterWriteLock();
            else
                release = false;

            int bucket = BucketForAddress(index);
            Tuple<T, Y> item = DataBuckets[bucket][index];
            DataBuckets[bucket].RemoveAt(index);

            int keymapAddress = GetHashCodeForType(item.Key);
            int valuemapAddress = GetHashCodeForType(item.Value);

            KeyMap[keymapAddress].Remove(index);
            ValueMap[valuemapAddress].Remove(index);

            if (release)
                LockSlim.ExitWriteLock();
        }

        public long GetIndexForItem(Tuple<T, Y> item)
        {
            LockSlim.EnterReadLock();

            long index = -1;
            if (PreferKeyMapAddresses)
            {
                int keymapAddress = GetHashCodeForType(item.Key);

                foreach (long address in KeyMap[keymapAddress])
                {
                    int bucket = BucketForAddress(address);
                    if (DataBuckets[bucket][address].Value.Equals(item.Value))
                    {
                        index = address;
                        break;
                    }
                }
            }
            else
            {
                int valueMapAddress = GetHashCodeForType(item.Value);

                foreach (long address in ValueMap[valueMapAddress])
                {
                    int bucket = BucketForAddress(address);
                    if (DataBuckets[bucket][address].Key.Equals(item.Key))
                    {
                        index = address;
                        break;
                    }
                }
            }

            LockSlim.ExitReadLock();

            if (index < 0)
                throw new KeyNotFoundException("The specified item cannot be found in the collection.");

            return index;
        }

        public bool ContainsItem(Tuple<T, Y> item, out long address)
        {
            try
            {
                address = GetIndexForItem(item);
                return true;
            }
            catch (KeyNotFoundException)
            {
                address = -1;
                return false;
            }
        }

        public bool ContainsItem(Tuple<T,Y> item)
        {
            return ContainsItem(item, out long p2);
        }

        public bool ContainsKey(T key, out Tuple<T, Y> item, out long address)
        {
            LockSlim.EnterReadLock();

            item = null;

            long index = -1;

            int keymapAddress = GetHashCodeForType(key);

            foreach (long addr in KeyMap[keymapAddress])
            {
                int bucket = BucketForAddress(addr);
                if (addr >= DataBuckets[bucket].Count + DataBuckets[bucket].Offset)
                    continue;
                if (DataBuckets[bucket][addr].Key.Equals(key))
                {
                    item = DataBuckets[bucket][addr];
                    index = addr;
                    break;
                }
            }

            LockSlim.ExitReadLock();

            if (index == -1)
            {
                item = null;
                address = -1;
                return false;
            }

            address = index;
            return true;
        }

        public bool ContainsKey(T key)
        {
            return ContainsKey(key, out Tuple<T, Y> p2, out long p3);
        }

        public bool ContainsValue(Y value, out Tuple<T, Y> item, out long address)
        {
            LockSlim.EnterReadLock();

            item = null;

            long index = -1;

            int valueMapAddress = GetHashCodeForType(value);

            foreach (long addr in ValueMap[valueMapAddress])
            {
                int bucket = BucketForAddress(addr);
                if (addr >= DataBuckets[bucket].Count + DataBuckets[bucket].Offset)
                    continue;
                if (DataBuckets[bucket][addr].Value.Equals(value))
                {
                    item = DataBuckets[bucket][addr];
                    index = addr;
                    break;
                }
            }

            LockSlim.ExitReadLock();

            if (index == -1)
            {
                item = null;
                address = -1;
                return false;
            }

            address = index;
            return true;
        }

        public bool ContainsValue(Y value)
        {
            return ContainsValue(value, out Tuple<T, Y> p2, out long p3);
        }

        protected virtual int GetHashCodeForType(T key)
        {
            int code = key.GetHashCode();
            if (code < 0)
                code *= -1;

            code /= HashCodeCompressionFactor;

            return code;
        }

        protected virtual int GetHashCodeForType(Y value)
        {
            int code = value.GetHashCode();
            if (code < 0)
                code *= -1;

            code /= HashCodeCompressionFactor;

            return code;
        }
        public virtual void Clear()
        {
            int hashCodeArraySize = int.MaxValue;
            if (CompressHashCodes) hashCodeArraySize = (hashCodeArraySize / HashCodeCompressionFactor) + 1;

            DataBuckets = new List<OffsettedList<Tuple<T, Y>>>(7192);
            KeyMap = new List<long>[hashCodeArraySize];
            ValueMap = new List<long>[hashCodeArraySize];

            DataBuckets.Add(new OffsettedList<Tuple<T, Y>>(0, BucketSize));

            for (int i = 0; i < KeyMap.Length; i++)
            {
                KeyMap[i] = new List<long>(4);
                ValueMap[i] = new List<long>(4);
            }
        }

    }
}
