using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RDHasher.CollisionChecker
{
    class Program
    {
        internal const int ITERATIONS = 7000000;
        internal const int BLOCK_SIZE = 72;
        internal const bool USE_HSD = true;
        internal const int TOTAL_THREADS = 16;
        internal const int UI_UPDATE_DELAY = 128;
        internal const int REPETITIONS = 50;

        internal static object ConsoleObject = new Object();
        internal static int LastLogAddress = 0;
        internal static FileStream CollisionFileStream;
        internal static StreamWriter CollisionFileWriter;
        internal static RDHash.CircularBuffer<float> AveragedHashPerSecond = new RDHash.CircularBuffer<float>(30);
        internal static int AveragedHashPerSecondCount = 0;
        internal static int CurrentRepetition = 0;

        internal static List<HasherBackgroundWorker> Threads;

        public static int TotalCollisions
        {
            get
            {
                int result = 0;
                foreach (HasherBackgroundWorker worker in Threads)
                {
                    result += worker.Collisions;
                }

                return result;
            }
        }

        public static int MaximumQueueLength
        {
            get
            {
                int result = 0;
                foreach (HasherBackgroundWorker worker in Threads)
                {
                    if (worker.MaximumQueueLength > result)
                    result = worker.MaximumQueueLength;
                }

                return result;
            }
        }

        public static bool IsRunning
        {
            get
            {
                foreach (HasherBackgroundWorker worker in Threads)
                    if (worker.IsRunning)
                        return true;

                return false;
            }
        }

        public static long TotalCompletedIterations
        {
            get
            {
                long result = 0;
                foreach (HasherBackgroundWorker worker in Threads)
                {
                    result += worker.IterationsCompleted;
                }

                return result;
            }
        }

        public static long TotalInputCollisions
        {
            get
            {
                long result = 0;
                foreach (HasherBackgroundWorker worker in Threads)
                {
                    result += worker.InputCollisions;
                }

                return result;
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("RDHash Library static collision check tool");
            Console.WriteLine("Blocksize is {0} bytes long.", BLOCK_SIZE.ToString("N0"));

            RDHash.RDHasher hasher = new RDHash.RDHasher();
            hasher.MinHashSize = 14;
            CollisionFileStream = new FileStream(".\\collisions.log", FileMode.Append, FileAccess.Write, FileShare.Read);
            CollisionFileWriter = new StreamWriter(CollisionFileStream, Encoding.ASCII, 4096, true);

            double n = Math.Pow(hasher.EncodeWheel.GlyphCount, hasher.MinHashSize);
            double k = ITERATIONS * REPETITIONS;
            double exponent = (-k * (k - 1)) / (2 * n);
            double chance = 1 - Math.Pow(Math.E, exponent);
            Console.WriteLine("Using {0} total CPU threads.", TOTAL_THREADS.ToString("N0"));
            Console.WriteLine("Clash chance: {0} %", (chance * 100).ToString("N5"));
            Console.WriteLine();
            Console.WriteLine();

            Random rng = new Random();
            for (int repetition = 1; repetition <= REPETITIONS; repetition++)
            {
                CurrentRepetition = repetition;
                LastLogAddress = Console.CursorTop;
                Stopwatch sw = new Stopwatch();
                UpdateStats(null, true);
                sw.Start();

                IDataSet hashes;
                if (USE_HSD)
                    hashes = new DoubleHashedDataSetWrapper(ITERATIONS, true);
                else
                    hashes = new HashSetWrapper(ITERATIONS);

                Threads = new List<HasherBackgroundWorker>();
                long iterations = ITERATIONS;
                while (iterations > 0)
                {
                    long slice = (ITERATIONS / TOTAL_THREADS);
                    var worker = new HasherBackgroundWorker(hasher, Math.Min(slice, iterations), hashes, BLOCK_SIZE, rng.Next(int.MinValue, int.MaxValue));
                    worker.PrintMessage += OnThreadPrintMessage;
                    worker.PrintCollisionMessage += OnThreadPrintCollisionMessage;
                    Threads.Add(worker);
                    iterations -= slice;
                }

                for (int i = 0; i < Threads.Count; i++)
                    Threads[i].Start();

            
                while (IsRunning)
                {
                    Thread.Sleep(UI_UPDATE_DELAY);

                    UpdateStats(Threads[0]);
                }

                sw.Stop();
                PrintMessage(string.Format("Pass {0} of {1}. {2} hashes calculated. {3} collisions found. {4}.{5} time spent.",
                    repetition.ToString("N0"),
                    REPETITIONS.ToString("N0"),
                    hashes.Count.ToString("N0"),
                    TotalCollisions.ToString("N0"),
                    sw.Elapsed.ToString("hh\\:mm\\:ss"),
                    sw.Elapsed.Milliseconds.ToString()));

                Threads.Clear();
                hashes.Clear();
                GC.Collect();
            }

            CollisionFileWriter.Flush();
            CollisionFileWriter.Dispose();
            CollisionFileStream.Flush();
            CollisionFileStream.Dispose();
            PrintMessage("");
            PrintMessage("Operation completed.");
            PrintMessage("- Press any key to exit -");

            Console.ReadKey();
        }

        internal static void OnThreadPrintMessage(HasherBackgroundWorker worker, string message)
        {
            PrintMessage(message);
        }

        internal static void OnThreadPrintCollisionMessage(HasherBackgroundWorker worker, string message, string key1, string key2, string value)
        {
            CollisionFileWriter.WriteLine(string.Format("{0} <--@--> {1} @===> {2}", key1, key2, value));
        }

        const int STATUS_LINES = 4;
        internal static void PrintMessage(string message)
        {
            lock (ConsoleObject)
            {
                Console.CursorTop = LastLogAddress;
                Console.CursorLeft = 0;

                Console.WriteLine(message);

                LastLogAddress = Console.CursorTop;

                if (Console.CursorTop == Console.WindowHeight + Console.WindowTop - STATUS_LINES)
                {
                    string b = new string(' ', Console.WindowWidth);
                    Console.Write(b);
                    Console.Write(b);
                    Console.Write(b);

                    Console.CursorTop -= STATUS_LINES;
                }
            }
        }

        private static long _lastCompletedIterations = 0;
        private static DateTime _lastStatsUpdate = DateTime.Now;
        internal static void UpdateStats(HasherBackgroundWorker worker, bool loading = false)
        {
            lock (ConsoleObject)
            {
                ConsoleColor fore = Console.ForegroundColor;
                ConsoleColor back = Console.BackgroundColor;

                Console.ForegroundColor = ConsoleColor.DarkBlue;
                Console.BackgroundColor = ConsoleColor.Gray;

                Console.CursorLeft = 0;
                Console.CursorTop = Console.WindowHeight + Console.WindowTop - STATUS_LINES;
                string message = "In: ";
                string input;
                string output = null;
                string inputTrimmed;

                DateTime now = DateTime.Now;

                if (loading)
                    Console.Write(message + "---");
                else
                {
                    input = worker.LastInputString;
                    output = worker.LastOutputString;
                    inputTrimmed = input;

                    if (inputTrimmed.Length + message.Length >= Console.WindowWidth - 1)
                        inputTrimmed = inputTrimmed.Substring(0, Console.WindowWidth - 1 - 3 - message.Length) + "...";
                    Console.Write(message + inputTrimmed);
                }

                while (Console.CursorLeft != 0) Console.Write(' ');

                if (loading)
                    Console.Write("Out: ---");
                else
                    Console.Write("Out: " + output);

                while (Console.CursorLeft != 0) Console.Write(' ');

                if (loading)
                {
                    Console.Write(" --- Loading ---");

                    while (Console.CursorLeft != 0) Console.Write(' ');

                    Console.Write("Please wait...");

                    _lastCompletedIterations = 0;
                    _lastStatsUpdate = now;
                }
                else
                {
                    long totalCompletedIters = TotalCompletedIterations;
                    float hashesps = (float)(((totalCompletedIters - _lastCompletedIterations)) / ((now - _lastStatsUpdate).TotalMilliseconds));
                    AveragedHashPerSecond.Insert(hashesps);
                    if (AveragedHashPerSecondCount < AveragedHashPerSecond.Capacity)
                        AveragedHashPerSecondCount++;

                    for (int i = 0; i < AveragedHashPerSecondCount; i++)
                        hashesps += AveragedHashPerSecond.ElementAt(i);

                    hashesps /= AveragedHashPerSecondCount;

                    TimeSpan eta = TimeSpan.FromSeconds((ITERATIONS - totalCompletedIters) / (hashesps * 1000.0f));
                    TimeSpan etatotal = TimeSpan.FromSeconds(((ITERATIONS * REPETITIONS) - ((CurrentRepetition - 1) * ITERATIONS) + totalCompletedIters) / (hashesps * 1000.0f));

                    Console.Write("Test {0} of {1}. {2} % complete. {3} in / {4} out collisions.",
                        totalCompletedIters.ToString("N0"),
                        ITERATIONS.ToString("N0"),
                        (((float)totalCompletedIters / (float)ITERATIONS) * 100.0f).ToString("N2"),
                        TotalInputCollisions.ToString("N0"),
                        TotalCollisions.ToString("N0")
                        );

                    while (Console.CursorLeft != 0) Console.Write(' ');

                    Console.Write("Pass {0} of {1}. {2} Mb used. {3} Avg KHashes/sec. [Qdepth max={4}]. ETA: Total: {5} [Pass: {6}]",
                        CurrentRepetition.ToString("N0"),
                        REPETITIONS.ToString("N0"),
                        (GC.GetTotalMemory(false) / 1000.0f / 1000.0f).ToString("N2"),
                        hashesps.ToString("N2"),
                        MaximumQueueLength == 0 ? "--" : MaximumQueueLength.ToString("N0"),
                        etatotal.ToString("hh\\:mm\\:ss"),
                        eta.ToString("hh\\:mm\\:ss")
                        );

                    _lastCompletedIterations = totalCompletedIters;
                    _lastStatsUpdate = now;
                }

                while (Console.CursorLeft < Console.WindowWidth - 1) Console.Write(' ');

                Console.ForegroundColor = fore;
                Console.BackgroundColor = back;
            }
        }
    }
}
