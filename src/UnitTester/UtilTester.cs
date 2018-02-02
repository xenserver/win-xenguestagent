using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

using xenguestlib.NicUtil;
using UTHelper;

namespace UnitTester
{
    /// <summary>
    /// Test class for NicUtil classs
    /// </summary>
    [TestClass]
    public class NicUtilTester
    {
        byte[] mac1;
        /// <summary>
        /// Constructor, init an mac byte array
        /// </summary>
        public NicUtilTester() 
        {
           mac1 = new byte[] { 0x32, 0x70, 0xd5, 0xa0, 0x5a, 0x21 };
        }

        /// <summary>
        /// Test the matchByteArray method
        ///      1. same byte content should return true
        ///      2. diff length, but same previous content should return true
        ///      3. diff content should return false
        /// </summary>
        [TestMethod]
        public void TestMatchByteArray()
        {

            byte[] arr2 = new byte[] { 0x32, 0x70, 0xd5, 0xa0, 0x5a, 0x21 };
            bool result = NicUtil.matchByteArray(mac1, arr2);
            Assert.IsTrue(result);

            arr2 = new byte[] { 0x32, 0x70, 0xd5, 0xa0, 0x5a, 0x21, 0xff };
            result = NicUtil.matchByteArray(mac1, arr2);
            Assert.IsTrue(result);

            arr2 = new byte[] { 0x32, 0x70, 0xd5, 0xa0, 0x5a, 0x22 };
            result = NicUtil.matchByteArray(mac1, arr2);
            Assert.IsFalse(result);
        }

        /// <summary>
        /// Test the getMacStringFromByteArray method
        ///     The returned mac string should be the same as expected
        /// </summary>
        [TestMethod]
        public void TestGetMacStringFromByteArray()
        {
            string expectedResult = UTFunctions.getMacString(mac1);
            string result = NicUtil.getMacStringFromByteArray(mac1);
            Assert.AreEqual(expectedResult, result);
        }

        /// <summary>
        ///  Test getByteArrayfromMacString method
        ///      The returned byte array should be the same as the string
        /// </summary>
        [TestMethod]
        public void TestFromMacStringToByte() 
        {
            const string fromStr = "32:70:d5:a0:5a:21";
            byte[] resultBytes = NicUtil.getByteArrayfromMacString(fromStr);
            Assert.IsTrue(resultBytes.SequenceEqual(mac1));
        }
    }
}
