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
    public class EncodingWheelTests
    {
        [TestMethod()]
        public void RotateTest()
        {
            char[] expected = new char[] { 'D', 'E', 'F', 'A', 'B', 'C' };
            EncodingWheel wheel = new EncodingWheel(new char[] {  'A', 'B', 'C', 'D', 'E', 'F'});
            wheel.Rotate(3);

            Assert.IsTrue(wheel.State.SequenceEqual<char>(expected));

            wheel.Rotate(-6);
            Assert.IsTrue(wheel.State.SequenceEqual<char>(expected));
        }

        [TestMethod()]
        public void ShuffleTest()
        {
            EncodingWheel wheel = new EncodingWheel(new char[] { 'A', 'B', 'C', 'D', 'E', 'F' });
            char[] expected = new char[] { 'D', 'B', 'F', 'C', 'A', 'E' };
            wheel.Shuffle(255874);

            Assert.IsTrue(wheel.State.SequenceEqual<char>(expected));
        }
    }
}