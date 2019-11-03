using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RDHash
{
    public class HashingResults
    {
        private int[] loopStepping;

        public string InputString { get; protected internal set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Returned array is a temporarily generated copy of the internal InputString")]
        public byte[] InputData
        {
            get { return Encoding.ASCII.GetBytes(InputString); }
            set { InputString = Encoding.ASCII.GetString(value); }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Returned array is a temporarily generated copy of the internal LoopStepping array")]
        public int[] LoopStepping
        {
            get => (int[])loopStepping.Clone();
            protected internal set => loopStepping = value;
        }
        public int InputLen => InputString.Length;
        public string OutputHash { get; protected internal set; }
        public int OutputLen => OutputHash.Length;
        public float InputOutputRatio => OutputLen / InputLen;
        public RDHasher Hasher { get; protected internal set; }
        public string SanitizedInput { get; protected internal set; }

        protected HashingResults(RDHasher instance, string input, string sanitizedInput, string output, int[] steplens)
        {
            Hasher = instance;
            InputString = input;
            OutputHash = output;
            LoopStepping = steplens;
            SanitizedInput = sanitizedInput;
        }

        protected internal HashingResults(RDHasher instance, string input)
        {
            Hasher = instance;
            InputString = input;
        }
    }
}
