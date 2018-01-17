using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net.NetworkInformation;


namespace xenwinsvc
{

    class MockNetworkInterface : NetworkInterface
    {
        PhysicalAddress _physicalAddress;

        public void SetPhysicalAddress(PhysicalAddress physicalAddress)
        {
            _physicalAddress = physicalAddress;
        }
        public override PhysicalAddress GetPhysicalAddress()
        {
            return _physicalAddress;
        }

        public override string Description
        {
            get { throw new NotImplementedException(); }
        }

        public override IPInterfaceProperties GetIPProperties()
        {
            throw new NotImplementedException();
        }

        public override IPv4InterfaceStatistics GetIPv4Statistics()
        {
            throw new NotImplementedException();
        }

        public override string Id
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsReceiveOnly
        {
            get { throw new NotImplementedException(); }
        }

        public override string Name
        {
            get { throw new NotImplementedException(); }
        }

        public override NetworkInterfaceType NetworkInterfaceType
        {
            get { throw new NotImplementedException(); }
        }

        public override OperationalStatus OperationalStatus
        {
            get { throw new NotImplementedException(); }
        }

        public override long Speed
        {
            get { throw new NotImplementedException(); }
        }

        public override bool Supports(NetworkInterfaceComponent networkInterfaceComponent)
        {
            throw new NotImplementedException();
        }

        public override bool SupportsMulticast
        {
            get { throw new NotImplementedException(); }
        }

        public MockNetworkInterface() { }


    }
    [TestClass]
    public class NetInfoTest
    {
        
        [TestMethod]
        public void TestmacsMatch()
        {
            byte[] testMac = new byte[] { 11, 22, 33, 44, 55, 66, 77};
            PhysicalAddress padd = new PhysicalAddress(testMac);
            MockNetworkInterface netInterface = new MockNetworkInterface();
            netInterface.SetPhysicalAddress(padd);
            System.Console.WriteLine(netInterface.GetPhysicalAddress().ToString());
            const string expectedMacStr = "11:22:33:44:55:66:77";

            VfInfo vifInfo = new VfInfo(null);
            Assert.IsTrue(vifInfo.macsMatch(expectedMacStr, netInterface));

        }
    }
}
