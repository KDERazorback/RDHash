using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSD
{
    public class Tuple<T, Y>
    {
        public T Key;
        public Y Value;

        public Tuple(T key, Y value)
        {
            Key = key;
            Value = value;
        }
    }
}
