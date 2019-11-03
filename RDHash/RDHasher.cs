using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RDHash
{
    public class RDHasher
    {
        // Define the encoding wheel
        /// <summary>
        /// Stores the <see cref="EncodingWheel"/> used to convert the resulting hash into a human-readable string
        /// </summary>
        public EncodingWheel EncodeWheel { get; set; } = new EncodingWheel(new char[] { '0', '1', '2', '3', '4', '5', '6', '7',
                                                    '8', '9', 'A', 'B', 'C', 'D', 'E', 'F',
                                                    'G', 'H', 'J', 'K', 'M', 'N', 'P', 'Q',
                                                    'R', 'S', 'T', 'U', 'W', 'X', 'Y', 'Z' }); // Len = 32. Indexes 0 thru 31

        /// <summary>
        /// Specifies the minimum amount of significant characters for the resulting hashes, excluding separation and readability characters
        /// </summary>
        public int MinHashSize { get; set; } = 8;

        /* MinHashSize is used to calculate the minimum limits of the hash function.
         * The result can still grow above this limit when required 
         * The minimum input is calculated using the following formulae:
         *      min = ((h - 2) * 2) - 1
         *      When min is the minimum input size, and h the MinHashSize specified
         */
        /// <summary>
        /// Returns the minimum input size for the current Hash configuration
        /// </summary>
        public int MinimumInputSize
        {
            get
            {
                return ((MinHashSize - 2) * 2) - 1;
            }
        }

        /// <summary>
        /// Specifies the minimum value accepted by the hash algorithm as a valid input for each position.
        /// Values smaller than this will be removed from the input string before calculation (Inclusive)
        /// </summary>
        public byte InputTrimmingLow { get; set; } = 0x30;
        /// <summary>
        /// Specifies the maximum value accepted by the hash algorithm as a valid input for each position.
        /// Values bigger than this will be removed from the input string before calculation (Inclusive)
        /// </summary>
        public byte InputTrimmingHigh { get; set; } = 0x5D;

        /// <summary>
        /// Indicates if the input processor should ignore casing for every input string, converting them to Upper Case before processing them
        /// </summary>
        public bool InputIgnoreCasing { get; set; } = true;

        /// <summary>
        /// Specifies if the output hash should be formatted into a more readable format by splitting it into smaller groups delimited by <see cref="ReadableFormattingChar"/>
        /// </summary>
        public bool ReadableFormatting { get; set; } = true;

        /// <summary>
        /// Specifies the character used to group output hashes into more readable strings. Usually dashes or underscores.
        /// This character should not be stored inside the current <see cref="EncodingWheel"/>
        /// </summary>
        public char ReadableFormattingChar { get; set; } = '-';

        /// <summary>
        /// Computes the hash of an specified input block, using the current hasher configuration and Encoding wheel
        /// </summary>
        /// <param name="input">Input block from which its hash will be calculated</param>
        /// <returns>A <see cref="HashingResults"/> object that holds the results of the hash calculation, as well as statistical information from the process</returns>
        /// <remarks>A hardcoded limit of 1024 bytes is in place to limit the size of input blocks. If you need to calculate a hash for a bigger dataset, then split
        /// the input data into smaller chunks and process them individually. The results could be concatenated or postprocessed to form a new hash. 
        /// </remarks>
        public HashingResults HashBlock(string input)
        {
            // COMMENTS ON THIS FUNCTION ARE PLACED ASUMING DEFAULT VALUES FOR EACH OBJECT'S PROPERTY
            // Expects an input string composed of characters between 0x30 (InputTrimmingLow) - 0x5D (InputTrimmingHigh) from the ASCII table up to 1024 bytes long
            // Lowercase characters (0x61 - 0x7A) are converted to upper case if defined (InputIgnoreCasing = true)
            // Remaining characters from 0x00 - 0x2E and 0x5E - 0xFF are ignored and removed from the input string (InputTrimmingLow, InputTrimmingHigh, InputIgnoreCasing = true)
            // The minimum input string required is 7 bytes long (MinimumInputSize)

            if (string.IsNullOrEmpty(input))
                throw new ArgumentNullException(nameof(input));

            if (input.Length > 1024)
                throw new InvalidOperationException("The input block is too large. A maximum of 1024 bytes is required since bigger calculations are not reliable\n" +
                    "If you require a hash of a bigger dataset, then split the input data into smaller blocks and further process the results.");

            if (input.Length < MinimumInputSize)
                throw new InvalidOperationException("The input block is too small. A minimum of " + MinimumInputSize.ToString("N0", CultureInfo.InvariantCulture) +
                    " bytes is required with the current configuration.");

            HashingResults results = new HashingResults(this, input);

            Stack<int> steplens = new Stack<int>();

            StringBuilder str;
            uint sum = 0;

            steplens.Push(input.Length);

            // Sanitize the input string
            input = SanitizeInput(input, out sum);

            steplens.Push(input.Length);

            results.SanitizedInput = input;

            if (input.Length < MinimumInputSize)
                throw new InvalidOperationException("The input block (after pre-processing) is too small. A minimum of " + MinimumInputSize.ToString("N0", CultureInfo.InvariantCulture) +
                    " bytes is required with the current configuration.");

            // Additive operation between N and K
            do
            {
                str = new StringBuilder(input.Length);
                for (int i = 0; i < input.Length; i++)
                {
                    uint delta = 0;
                    if ((i + 1) % 2 == 0)
                        delta = (uint)Math.Abs(input[i] - input[i - 1]);
                    else if (i == input.Length - 1)
                        delta = input[i];
                    else
                        continue;

                    delta -= (uint)((int)(delta / EncodeWheel.GlyphCount) * EncodeWheel.GlyphCount);

                    str.Append((char)delta);
                }
                steplens.Push(input.Length);
                input = str.ToString();
            } while (input.Length > MinimumInputSize);

            Debug.Assert(input.Length >= (MinHashSize - 2)); // Resulting input string should be at least this long to ensure minimum hash len

            // Calculate checksum1 from the input string
            uint checksum1 = 0;
            if (sum % 2 == 0 && EncodeWheel.EncodeBitsRequired >= 1) checksum1 |= 0b00000001;
            if (sum % 3 == 0 && EncodeWheel.EncodeBitsRequired >= 2) checksum1 |= 0b00000010;
            if (sum % 5 == 0 && EncodeWheel.EncodeBitsRequired >= 3) checksum1 |= 0b00000100;
            if (sum % 7 == 0 && EncodeWheel.EncodeBitsRequired >= 4) checksum1 |= 0b00001000;
            if (sum % 11 == 0 && EncodeWheel.EncodeBitsRequired >= 5) checksum1 |= 0b00010000;
            if (sum % 13 == 0 && EncodeWheel.EncodeBitsRequired >= 6) checksum1 |= 0b00100000;
            if (sum % 17 == 0 && EncodeWheel.EncodeBitsRequired >= 7) checksum1 |= 0b01000000;
            if (sum % 19 == 0 && EncodeWheel.EncodeBitsRequired >= 8) checksum1 |= 0b10000000;

            // Calculate checksum2 from the input string
            uint checksum2 = 0;
            if (sum % 23 == 0 && EncodeWheel.EncodeBitsRequired >= 1) checksum2 |= 0b00000001;
            if (sum % 29 == 0 && EncodeWheel.EncodeBitsRequired >= 2) checksum2 |= 0b00000010;
            if (sum % 31 == 0 && EncodeWheel.EncodeBitsRequired >= 3) checksum2 |= 0b00000100;
            if (sum % 37 == 0 && EncodeWheel.EncodeBitsRequired >= 4) checksum2 |= 0b00001000;
            if (sum % 41 == 0 && EncodeWheel.EncodeBitsRequired >= 5) checksum2 |= 0b00010000;
            if (sum % 43 == 0 && EncodeWheel.EncodeBitsRequired >= 6) checksum2 |= 0b00100000;
            if (sum % 47 == 0 && EncodeWheel.EncodeBitsRequired >= 7) checksum2 |= 0b01000000;
            if (sum % 53 == 0 && EncodeWheel.EncodeBitsRequired >= 8) checksum2 |= 0b10000000;

            // OR both checksums and calculate the difference
            checksum1 |= checksum2;

            // Get the complement of the checksum
            checksum1 ^= (uint)Math.Pow(2, EncodeWheel.EncodeBitsRequired) - 1;

            // Get the hash checksum
            uint checksum3 = ComputePartialHashChecksum(input);

            // Append the checksums to the generated string
            input = (char)checksum1 + input + (char)checksum3;

            // Generate a new string using the encoding wheel
            input = TransposeIndices(input);

            // Add dashes for better readability
            if (ReadableFormatting)
            {
                int dashesEvery = 4;
                if (input.Length % 3 == 0 && input.Length / 3 <= 4) dashesEvery = 3; // Calculate how much positions between dashes
                int groups = (int)(input.Length / dashesEvery);
                int offby = (input.Length - ((int)(input.Length / dashesEvery) * dashesEvery)); // Calculate how much positions are left outside of dashes
                int adjustnode = (groups / 2); // Get the group that will hold the orphan positions

                // Add dashes leaving the bigger group on the middle of the chain
                str.Clear();
                int count = 0;
                int groupIndex = 0;
                for (int i = 0; i < input.Length; i++)
                {
                    str.Append(input[i]);
                    count++;

                    if (count == dashesEvery && i < input.Length - 1)
                    {
                        str.Append(ReadableFormattingChar);
                        groupIndex += 1;

                        count = 0;

                        if (groupIndex == adjustnode)
                            count = -offby;
                    }
                }
                input = str.ToString();
            }

            // Return the generated string
            steplens.Push(str.Length);
            results.LoopStepping = steplens.ToArray();
            results.OutputHash = input;
            return results;
        }

        protected virtual uint ComputePartialHashChecksum(string hash)
        {
            if (string.IsNullOrEmpty(hash))
                throw new ArgumentNullException(nameof(hash));

            uint sum = 0;
            uint checksum3 = 0;
            // Calculate sum of the generated string
            for (int i = 0; i < hash.Length; i++)
                sum += hash[i];

            // Calculate checksum3 from the generated string
            if (sum % 2 == 0 && EncodeWheel.EncodeBitsRequired >= 1) checksum3 |= 0b00000001;
            if (sum % 3 == 0 && EncodeWheel.EncodeBitsRequired >= 2) checksum3 |= 0b00000010;
            if (sum % 5 == 0 && EncodeWheel.EncodeBitsRequired >= 3) checksum3 |= 0b00000100;
            if (sum % 7 == 0 && EncodeWheel.EncodeBitsRequired >= 4) checksum3 |= 0b00001000;
            if (sum % 11 == 0 && EncodeWheel.EncodeBitsRequired >= 5) checksum3 |= 0b00010000;
            if (sum % 13 == 0 && EncodeWheel.EncodeBitsRequired >= 6) checksum3 |= 0b00100000;
            if (sum % 17 == 0 && EncodeWheel.EncodeBitsRequired >= 7) checksum3 |= 0b01000000;
            if (sum % 19 == 0 && EncodeWheel.EncodeBitsRequired >= 8) checksum3 |= 0b10000000;

            // Get the complement of the checksum
            checksum3 ^= (uint)Math.Pow(2, EncodeWheel.EncodeBitsRequired) - 1;

            return checksum3;
        }

        /// <summary>
        /// Computes the hash of an specified input block, using the current hasher configuration and Encoding wheel
        /// </summary>
        /// <param name="input">Input block from which its hash will be calculated</param>
        /// <returns>The resulting hash string with formatting pre-applied to it</returns>
        /// <remarks>A hardcoded limit of 1024 bytes is in place to limit the size of input blocks. If you need to calculate a hash for a bigger dataset, then split
        /// the input data into smaller chunks and process them individually. The results could be concatenated or postprocessed to form a new hash. 
        /// </remarks>
        public string ComputeHash(string input)
        {
            return HashBlock(input).OutputHash;
        }

        /// <summary>
        /// Sanitizes an input string for hash processing, removing unneeded values and applying formatting rules.
        /// </summary>
        /// <param name="input">Input dataset to be formatted</param>
        /// <param name="sum">Sum of all values present on the dataset</param>
        /// <returns>A new value with all formatting rules applied to it</returns>
        protected internal string SanitizeInput(string input, out uint sum)
        {
            if (string.IsNullOrEmpty(input))
                throw new ArgumentNullException(nameof(input));

            // Remove blank spaces at the start and end of the string
            // Convert to Upper-case
            string sanitized = input.Trim();

            if (InputIgnoreCasing)
                sanitized = sanitized.ToUpperInvariant();

            sum = 0;
            StringBuilder str = new StringBuilder(input.Length);

            // Remove characters outside of range and calculate the sum of the remaining ones, after substracting 0x2F from each to align them at 0x00 (InputTrimmingLow - 0x01)
            // Each two characters added N and K, substract N from K, and then substract 31 from the result until its smaller or equal than 31.
            // If the last item on the chain is odd placed, then substract 31 from it until its smaller or equal than 31, and append it to the result.
            // Repeat the last step until the operation generates an string that is up to 14 bytes long.
            for (int i = 0; i < sanitized.Length; i++)
            {
                if (sanitized[i] >= InputTrimmingLow && sanitized[i] <= InputTrimmingHigh)
                {
                    sum += (uint)sanitized[i] - InputTrimmingLow;
                    str.Append(sanitized[i]);
                }
            }

            return str.ToString();
        }

        /// <summary>
        /// Sanitizes an input string for hash processing, removing unneeded values and applying formatting rules.
        /// </summary>
        /// <param name="input">Input dataset to be formatted</param>
        /// <returns>A new value with all formatting rules applied to it</returns>
        public string SanitizeInput(string input)
        {
            return SanitizeInput(input, out uint p2);
        }

        /// <summary>
        /// Sanitizes an input hash for further validation and processing, removing unneeded delimiters and applying an standardized format
        /// </summary>
        /// <param name="hash">Input hash to be formatted</param>
        /// <returns>A new standardized hash with all formatting rules applied to it</returns>
        public string SanitizeHash(string hash)
        {
            if (string.IsNullOrEmpty(hash))
                throw new ArgumentNullException(nameof(hash));

            StringBuilder str = new StringBuilder(hash.Length);
            GlyphCollection glyphs = EncodeWheel.Glyphs;
            glyphs = new GlyphCollection(new string(glyphs.ToArray()).ToUpperInvariant().ToCharArray());
            hash = hash.ToUpperInvariant();
            for (int i = 0; i < hash.Length; i++)
            {
                if (glyphs.Contains(hash[i]))
                    str.Append(hash[i]);
            }

            return str.ToString().ToUpperInvariant();
        }

        /// <summary>
        /// Checks and returns the validity of an input hash against its embedded check-code, returning true if matches or false if the hash appears to be invalid
        /// </summary>
        /// <param name="hash">Input hash to validate</param>
        /// <returns>True if the hash contains a valid check-code for its data, false otherwise</returns>
        public bool ValidateHash(string hash)
        {
            if (string.IsNullOrEmpty(hash))
                throw new ArgumentNullException(nameof(hash));

            hash = SanitizeHash(hash);

            uint computedCheckCode = ComputePartialHashChecksum(TransposeGlyphs(hash.Substring(1, hash.Length - 2)));
            uint actualCheckcode = (uint)TransposeGlyph(hash[hash.Length - 1]);

            return actualCheckcode == computedCheckCode;
        }

        /// <summary>
        /// Compares two hashes and returns a value indicating if both are equal or not, this method doesnt take into account if the hashes are invalid
        /// </summary>
        /// <param name="hash1">First hash to compare</param>
        /// <param name="hash2">Second hash to compare</param>
        /// <returns>True if both hashes match, otherwise false</returns>
        public bool CompareHashes(string hash1, string hash2)
        {
            hash1 = SanitizeHash(hash1);
            hash2 = SanitizeHash(hash2);

            return string.Equals(hash1, hash2, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Converts an string of glyphs into an string of indices using the current <see cref="EncodingWheel"/>. This method is Case-sensitive
        /// </summary>
        /// <param name="input">Input string of characters to transpose into indices for the <see cref="EncodingWheel"/></param>
        /// <returns>A transposed string of indices for the <see cref="EncodingWheel"/> that represents each Glyph present on the input</returns>
        public virtual string TransposeGlyphs(string input)
        {
            if (string.IsNullOrEmpty(input))
                throw new ArgumentNullException(nameof(input));

            StringBuilder str = new StringBuilder(input.Length);
            GlyphCollection glyphs = EncodeWheel.Glyphs;

            for (int i = 0; i < input.Length; i++)
            {
                for (int x = 0; x < glyphs.Count; x++)
                {
                    if (glyphs[x] == input[i])
                    {
                        str.Append((char)x);
                        break;
                    }
                }
            }

            return str.ToString();
        }

        /// <summary>
        /// Converts a glyph character into an index of the current <see cref="EncodingWheel"/>. This method is Case-sensitive
        /// </summary>
        /// <param name="glyph">Input character that will be transposed into an <see cref="EncodingWheel"/> index</param>
        /// <returns>An index of the <see cref="EncodingWheel"/> where the input glyph is located</returns>
        public virtual char TransposeGlyph(char glyph)
        {
            GlyphCollection glyphs = EncodeWheel.Glyphs;

            for (int x = 0; x < glyphs.Count; x++)
            {
                if (glyphs[x] == glyph)
                {
                    return (char)x;
                }
            }

            throw new IndexOutOfRangeException("The input glyph cannot be found on the current Encoding Wheel.");
        }

        /// <summary>
        /// Converts an index of the current <see cref="EncodingWheel"/> into a glyph character.
        /// </summary>
        /// <param name="index">Input index of the <see cref="EncodingWheel"/> to convert into a glyph character</param>
        /// <returns>A glyph from the current <see cref="EncodingWheel"/> pointed by the input index</returns>
        public virtual char TransposeIndex(char index)
        {
            return EncodeWheel[index];
        }

        /// <summary>
        /// Converts an string of indices from the current <see cref="EncodingWheel"/> into an string of characters
        /// </summary>
        /// <param name="input">Input string of indices from the current <see cref="EncodingWheel"/> to transpose into an string of characters</param>
        /// <returns>A transposed string of characters generated using the current <see cref="EncodingWheel"/> and the given string of indices</returns>
        public virtual string TransposeIndices(string input)
        {
            if (string.IsNullOrEmpty(input))
                throw new ArgumentNullException(nameof(input));

            StringBuilder str = new StringBuilder(input.Length);
            GlyphCollection glyphs = EncodeWheel.Glyphs;

            for (int i = 0; i < input.Length; i++)
                str.Append(glyphs[(int)input[i]]);

            return str.ToString();
        }

    }
}
