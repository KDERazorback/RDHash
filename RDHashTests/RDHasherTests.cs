using Microsoft.VisualStudio.TestTools.UnitTesting;
using RDHash;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RDHash.Tests
{
    [TestClass()]
    public class RDHasherTests
    {
        [TestMethod()]
        public void CompareHashesTest()
        {
            RDHasher hasher = new RDHasher();
            string hash1 = "CKPPN-APNS-47ASK-3FFF";
            string hash2 = "CKPPNAP-   NS   47 A s k 3 F  Ff";

            Assert.IsTrue(hasher.CompareHashes(hash1, hash2));
        }

        [TestMethod()]
        public void ValidateHashTest()
        {
            RDHasher hasher = new RDHasher();

            string hash = hasher.ComputeHash("abcdefghijklmno 1234 77765 0987654321");

            Assert.IsTrue(hasher.ValidateHash(hash));
        }

        [TestMethod()]
        public void SanitizeHashTest()
        {
            RDHasher hasher = new RDHasher();
            string hash = "CKPPNAP-   NS   47 A s k 3 F  Ff";

            Assert.IsTrue(string.Equals(hasher.SanitizeHash(hash), "CKPPNAPNS47ASK3FFF", StringComparison.Ordinal));
        }
    }
}