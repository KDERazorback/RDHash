using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace RDHash
{
    /// <summary>
    /// Defines an set of characters user for translation of byte values into strings as part of Hash calculations
    /// </summary>
    [DataContract(Name = "EncodingWheel", Namespace = "http://razorsoftware.tk/schemas/2019/10/RDHash/1.0")]
    public class EncodingWheel
    {
        private GlyphCollection glyphs;


        /// <summary>
        /// Stores the set of glyphs contained inside the encoding wheel
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Returned array is a shallow copy of the internal Glyph array")]
        [DataMember(Name = "Glyphs")]
        public GlyphCollection Glyphs
        {
            get
            {
                GlyphCollection o = new GlyphCollection(glyphs);
                return o;
            }
            set => glyphs = value; 
        }
        /// <summary>
        /// Stores the amount of positions that the wheel is rotated, in either direction
        /// </summary>
        [DataMember]
        public int Rotation { get; set; }

        /// <summary>
        /// Access a glyph at the specified position inside the encoding wheel. This doesnt take into account the wheel rotation.
        /// </summary>
        /// <param name="index">Index of the glyph that will be accessed</param>
        /// <returns>The Glyph at the specified position inside the wheel</returns>
        public char this[int index]
        {
            get
            {
                return Glyphs[index];
            }
            set
            {
                Glyphs[index] = value;
            }
        }
        /// <summary>
        /// Returns a flyph at the specified position inside the encoding wheel. A second parameter indicates if the Wheel's Rotation should be pre-applied before getting/setting the value
        /// </summary>
        /// <param name="index">Index of the glyph that will be accessed</param>
        /// <param name="rotation">A value indicating if the wheel should be rotated before performing the operation</param>
        /// <returns>The Glyph at the specified position inside the wheel</returns>
        public virtual char this[int index, bool rotation]
        {
            get
            {
                int address = index;

                if (rotation)
                {
                    address += Rotation;
                    while (address >= GlyphCount)
                        address -= GlyphCount;

                    while (address < 0)
                        address += GlyphCount;
                }

                return this[address];
            }
            set
            {
                int address = index;

                if (rotation)
                {
                    address += Rotation;
                    while (address >= GlyphCount)
                        address -= GlyphCount;

                    while (address < 0)
                        address += GlyphCount;
                }

                Glyphs[address] = value;
            }
        }

        /// <summary>
        /// Rotates the encoding wheel by an specified amount of positions, in either forward or backward direction
        /// </summary>
        /// <param name="amount"></param>
        public virtual void Rotate(int amount)
        {
            Rotation += amount;
        }


        /// <summary>
        /// Returns a view of the current status of encoding wheel, with its set rotation pre-applied.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Returned array is a temporarily generated copy of the internal state array")]
        public char[] State
        {
            get
            {
                CircularBuffer<char> buffer = new CircularBuffer<char>(Glyphs.Count);
                foreach (char c in Glyphs)
                    buffer.InsertBackwards(c);

                buffer.Rotate(Rotation);

                return buffer.ToArray();
            }
        }

        /// <summary>
        /// Returns the amount of glyphs in the current Encoding Wheel
        /// </summary>
        public int GlyphCount => Glyphs.Count;

        /// <summary>
        /// Returns the amount of bits required to encode the hash using the provided glyphs
        /// </summary>
        public int EncodeBitsRequired
        {
            get
            {
                double v = Math.Log(GlyphCount, 2);
                if ((int)v < v)
                    return (int)v + 1;
                else
                    return (int)v;
            }
        }

        /// <summary>
        /// Creates a new Encoding Wheel using the specified Glyph set.
        /// </summary>
        /// <param name="glyphs">Set of glyphs to use on the new encoding wheel</param>
        public EncodingWheel(char[] glyphs)
        {
            Glyphs = new GlyphCollection(glyphs);
        }

        /// <summary>
        /// Returns a list of the glyphs inside the encoding wheel, taking into account for wheel's rotation.
        /// </summary>
        /// <returns>An string of glyphs for the current wheel state</returns>
        public override string ToString()
        {
            return new string(State);
        }

        public virtual void Shuffle(int seed)
        {
            Random rnd = new Random(seed);
            int count = Glyphs.Count;
            GlyphCollection glyphs = Glyphs;

            char c;
            while (count > 1)
            {
                count--;

                int k = rnd.Next(0, count + 1);

                c = glyphs[k];
                glyphs[k] = glyphs[count];
                glyphs[count] = c;
            }

            Glyphs = glyphs;
        }

        /// <summary>
        /// Pseudo-radomizes the <see cref="Glyphs"/>'s order using the Fisher–Yates algorithm on this encoding wheel and returns the seed used on the operation
        /// </summary>
        /// <returns>The seed used on the pseudo-random operation</returns>
        public virtual int Shuffle()
        {
            Random rnd = new Random();
            int seed = rnd.Next(int.MinValue, int.MaxValue);

            Shuffle(seed);
            return seed;
        }

        /// <summary>
        /// Serializes this Encoding wheel into an XML String
        /// </summary>
        /// <returns>An string representation of this encoding wheel's parameters</returns>
        public virtual string ToXmlString()
        {
            return ToXmlString(this);
        }

        /// <summary>
        /// Serializes the specified Encoding wheel into an XML String
        /// </summary>
        /// <param name="wheel">Encoding wheel to be serialized</param>
        /// <returns>An string representation of the specified Encoding wheel's parameters</returns>
        public static string ToXmlString(EncodingWheel wheel)
        {
            StringBuilder str = new StringBuilder();
            DataContractSerializer ser = new DataContractSerializer(typeof(EncodingWheel));
            XmlWriter writer = XmlWriter.Create(str, new XmlWriterSettings()
            {
#if DEBUG 
                Indent = true,
#endif
                CloseOutput = false 
            });
                ser.WriteObject(writer, wheel);
            writer.Flush();
            writer.Close();

            return str.ToString();
        }

        /// <summary>
        /// Deserializes an Encoding wheel from an input XML String
        /// </summary>
        /// <param name="xml">XML String to deserialize into an Encoding wheel</param>
        /// <returns>A deserialized Encoding wheel from the input XML string</returns>
        public static EncodingWheel FromXmlString(string xml)
        {
            EncodingWheel wheel;
            DataContractSerializer ser = new DataContractSerializer(typeof(EncodingWheel));
            using (StringReader reader = new StringReader(xml))
                using (XmlReader xmlreader = XmlReader.Create(reader))
                    wheel = (EncodingWheel)ser.ReadObject(xmlreader);

            return wheel;
        }

        /// <summary>
        /// Tries to Deserialize an Encoding wheel from an specfied input XML String
        /// </summary>
        /// <param name="xml">XML String to try to deserialize into an Encoding wheel</param>
        /// <param name="wheel">A deserialized Encoding wheel from the input XML string, it its valid, or null if the input string is invalid</param>
        /// <returns>A value indicating if the Deserialization process was completed successfully (True) or not (False)</returns>
        public static bool TryFromXmlString(string xml, out EncodingWheel wheel)
        {
            try
            {
                wheel = FromXmlString(xml);
            } catch (Exception)
            {
                wheel = null;
                return false;
            }

            return true;
        }
    }
}
