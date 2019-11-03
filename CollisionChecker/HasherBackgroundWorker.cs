using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RDHasher.CollisionChecker
{
    public class HasherBackgroundWorker
    {
        public Thread ThisThread { get; protected set; }
        public long Iterations { get; protected set; }
        public IDataSet HashMap { get; protected set; }
        public long IterationsCompleted { get; protected set; }
        public float Progress => (IterationsCompleted / Iterations) * 100.0f;
        public int BlockSize { get; set; }
        public int Collisions { get; protected set; }
        public int InputCollisions { get; protected set; }
        public RDHash.RDHasher Hasher { get; protected set; }
        public bool IsRunning => ThisThread.IsAlive;
        public string LastInputString { get; protected set; }
        public string LastOutputString { get; protected set; }
        protected Random Rnd { get; set; }
        public int MaximumQueueLength => HashMap.MaximumQueueLength;

        public delegate void PrintMessageDelegate(HasherBackgroundWorker sender, string message);
        public delegate void PrintCollisionMessageDelegate(HasherBackgroundWorker sender, string message, string key1, string key2, string value);
        public event PrintMessageDelegate PrintMessage;
        public event PrintCollisionMessageDelegate PrintCollisionMessage;

        public HasherBackgroundWorker(RDHash.RDHasher hasher, long iterations, IDataSet hashmap, int blockSize, int seed)
        {
            HashMap = hashmap;
            BlockSize = blockSize;
            Iterations = iterations;
            Hasher = hasher;
            Rnd = new Random(seed);
            ThisThread = new Thread(DoThread);
            ThisThread.Name = "BackgroundThread_worker";
            ThisThread.Priority = ThreadPriority.BelowNormal;
            ThisThread.IsBackground = true;
        }

        public void Start()
        {
            ThisThread.Start();
        }

        protected virtual void DoThread()
        {
            for (int i = 1; i <= Iterations; i++)
            {
                IterationsCompleted = i - 1;
                string input = GetString(BlockSize);
                while (HashMap.ContainsKey(Hasher.SanitizeInput(input)))
                {
                    InputCollisions++;
                    input = GetString(BlockSize);
                }

                string key = Hasher.SanitizeInput(input);
                string value = Hasher.ComputeHash(input);

                if (HashMap.ContainsValue(value))
                {
                    Collisions++;

                    HashMap.ContainsValue(value, out HSD.Tuple<string, string> item, out long address);

                    string confirmed;
                    if (string.Equals(Hasher.ComputeHash(key), Hasher.ComputeHash(item.Key), StringComparison.OrdinalIgnoreCase))
                        confirmed = "*C*";
                    else
                        confirmed = "";

                    OnCollisionMessage(string.Format("Collision: {0} || {1} ==> {2} {3}", item.Key, key, value, confirmed), item.Key, key, value);
                }
                else
                    HashMap.Add(new HSD.Tuple<string, string>(key, value));

                LastInputString = input;
                LastOutputString = value;
            }
            IterationsCompleted = Iterations;
        }

        protected void OnPrintMessage(string message)
        {
            PrintMessage?.Invoke(this, message);
        }

        protected void OnCollisionMessage(string message, string key1, string key2, string value)
        {
            PrintCollisionMessage?.Invoke(this, message, key1, key2, value);
        }

        internal string GetString(int len)
        {
            // Generates an string composed of characters between 0x20 - 0x7E from the ASCII table, up to an specified length
            StringBuilder str = new StringBuilder(len);

            while (str.Length < len)
            {
                int set = Rnd.Next(0, 3);

                switch (set)
                {
                    case 0:
                        str.Append((char)Rnd.Next(48, 57));
                        break;
                    case 1:
                        str.Append((char)Rnd.Next(65, 90));
                        break;
                    case 2:
                        str.Append((char)Rnd.Next(97, 122));
                        break;
                }

                if (Rnd.NextDouble() > 0.90)
                    str.Append(" ");
            }

            return str.ToString();
        }
    }
}
