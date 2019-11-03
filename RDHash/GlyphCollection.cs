using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace RDHash
{
    /// <summary>
    /// Stores a collection of Glyphs used by an <see cref="EncodingWheel"/>
    /// </summary>
    [CollectionDataContract(Name = "GlyphCollection", Namespace = "http://razorsoftware.tk/schemas/2019/10/RDHash/1.0")]
    public class GlyphCollection : List<char>
    {
        public GlyphCollection() { }

        public GlyphCollection(IEnumerable<char> glyphs) : base(glyphs) { }
    }
}
