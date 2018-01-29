using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using xenwinsvc;
using System.Management;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Text;
using UTHelper;

namespace XenWinSvcTester
{
    /// <summary>
    /// Inherit from UnicastIPAddressInformation to customize the IP address infomation
    /// </summary>
    public class MockIPAddressInformation : UnicastIPAddressInformation
    {

        IPAddress addr;
        public override IPAddress Address
        {
            get { return addr; }
        }

        /// <summary>
        /// Set the ip address of the IPAddressInformation
        /// </summary>
        /// <param name="addr">address in the information struct</param>
        public void SetIPAddress(IPAddress addr)
        {
            this.addr = addr;
        }

        public override bool IsDnsEligible
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsTransient
        {
            get { throw new NotImplementedException(); }
        }


        public override long AddressPreferredLifetime
        {
            get { throw new NotImplementedException(); }
        }

        public override long AddressValidLifetime
        {
            get { throw new NotImplementedException(); }
        }

        public override long DhcpLeaseLifetime
        {
            get { throw new NotImplementedException(); }
        }

        public override DuplicateAddressDetectionState DuplicateAddressDetectionState
        {
            get { throw new NotImplementedException(); }
        }

        public override IPAddress IPv4Mask
        {
            get { throw new NotImplementedException(); }
        }

        public override PrefixOrigin PrefixOrigin
        {
            get { throw new NotImplementedException(); }
        }

        public override SuffixOrigin SuffixOrigin
        {
            get { throw new NotImplementedException(); }
        }
    }
    /// <summary>
    /// Inherit from UnicastIPAddressInformationCollection to customize the collection operatoin
    /// </summary>
    public class MockUnicastIPAddressInformationCollection : UnicastIPAddressInformationCollection 
    {
        /// <summary>
        /// Internal List to store the UnicastIPAddressInformation
        /// </summary>
        private List<UnicastIPAddressInformation> addrs;

        /// <summary>
        /// Constructor
        /// </summary>
        public MockUnicastIPAddressInformationCollection()
        {
            addrs = new List<UnicastIPAddressInformation>();
        }

        /// <summary>
        /// Add UnicastIPAddressInformation into collection
        /// </summary>
        /// <param name="addr">UnicastIPAddressInformation to be added</param>
        override public void  Add(UnicastIPAddressInformation addr)
        {
            addrs.Add(addr);
        }

        /// <summary>
        /// Support the iterator
        /// </summary>
        /// <returns></returns>
        override public IEnumerator<UnicastIPAddressInformation> GetEnumerator() 
        {
            return addrs.GetEnumerator();
        }
    }

    /// <summary>
    /// Inherit the IPInterfaceProperties to customize the internal IPAddressInformationCollection
    /// </summary>
    public class MockIPInterfaceProperties : IPInterfaceProperties 
    {
        /// <summary>
        /// Internal structrue to store IPAddressInformationCollection
        /// </summary>
        MockUnicastIPAddressInformationCollection ipAddressColl;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="ipColl">internal MockUnicastIPAddressInformationCollection</param>
        public MockIPInterfaceProperties(MockUnicastIPAddressInformationCollection ipColl) 
        {
            this.ipAddressColl = ipColl;
        }

        public override IPAddressInformationCollection AnycastAddresses
        {
            get { throw new NotImplementedException(); }
        }

        public override IPAddressCollection DhcpServerAddresses
        {
            get { throw new NotImplementedException(); }
        }

        public override IPAddressCollection DnsAddresses
        {
            get { throw new NotImplementedException(); }
        }

        public override string DnsSuffix
        {
            get { throw new NotImplementedException(); }
        }

        public override GatewayIPAddressInformationCollection GatewayAddresses
        {
            get { throw new NotImplementedException(); }
        }

        public override IPv4InterfaceProperties GetIPv4Properties()
        {
            throw new NotImplementedException();
        }

        public override IPv6InterfaceProperties GetIPv6Properties()
        {
            throw new NotImplementedException();
        }

        public override bool IsDnsEnabled
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsDynamicDnsEnabled
        {
            get { throw new NotImplementedException(); }
        }

        public override MulticastIPAddressInformationCollection MulticastAddresses
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// All the Unicast Addresses Information
        /// </summary>
        public override UnicastIPAddressInformationCollection UnicastAddresses
        {
            get 
            {
                return this.ipAddressColl;
            }
        }

        public override IPAddressCollection WinsServersAddresses
        {
            get { throw new NotImplementedException(); }
        }
    } 

    /// <summary>
    /// Inherit from NetworkInterface to customize the PhysicalAddress, IPInterfaceProperties
    /// </summary>
    public class MockNetworkInterface : NetworkInterface 
    {

        /// <summary>
        /// Customized PhysicalAddress
        /// </summary>
        private PhysicalAddress phyAddress;
        /// <summary>
        /// Customized IPInterfaceProperties
        /// </summary>
        MockIPInterfaceProperties properties;
        /// <summary>
        /// Customized name, description
        /// </summary>
        private string name, desc;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name">name of the NIC</param>
        /// <param name="desc">desc of the NIC</param>
        public MockNetworkInterface(string name, string desc) 
        {
            this.name = name;
            this.desc = desc;
        }
        /// <summary>
        /// Get the description
        /// </summary>
        public override string Description
        {
            get
            {
                return desc;
            }
        }
        /// <summary>
        /// Get the IPInterfaceProperties
        /// </summary>
        /// <returns></returns>
        public override IPInterfaceProperties GetIPProperties()
        {
            return properties;
        }
        /// <summary>
        /// Set the IPInterfaceProperties
        /// </summary>
        /// <param name="properties"></param>
        public void SetIPProperties(MockIPInterfaceProperties properties) 
        {
            this.properties = properties;
        }

        public override IPv4InterfaceStatistics GetIPv4Statistics()
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Get PhysicalAddress
        /// </summary>
        /// <returns></returns>
        public override PhysicalAddress GetPhysicalAddress()
        {
            return phyAddress;
        }
        /// <summary>
        /// Set PhysicalAddress
        /// </summary>
        /// <param name="add"></param>
        public void SetPhysicalAddress(PhysicalAddress add) 
        {
            this.phyAddress = add;      
        }

        public override string Id
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsReceiveOnly
        {
            get { throw new NotImplementedException(); }
        }
        /// <summary>
        /// Get name of the NIC
        /// </summary>
        public override string Name
        {
            get
            {
                return name;
            }
        }

        public override NetworkInterfaceType NetworkInterfaceType
        {
            get
            {
                return NetworkInterfaceType.Ethernet;
            }
        }

        public override OperationalStatus OperationalStatus
        {
            get { throw new NotImplementedException(); }
        }

        public override long Speed
        {
            get { throw new NotImplementedException(); }
        }
        /// <summary>
        /// Whether this NIC support the networkInterfaceComponent (ipv4, ipv6 etc)
        /// </summary>
        /// <param name="networkInterfaceComponent"></param>
        /// <returns>return true if any IP Address support, otherwise false</returns>
        public override bool Supports(NetworkInterfaceComponent networkInterfaceComponent)
        {
            AddressFamily family = AddressFamily.InterNetwork;
            if (networkInterfaceComponent == NetworkInterfaceComponent.IPv6) 
            {
                family = AddressFamily.InterNetworkV6;
            }
            UnicastIPAddressInformationCollection uniIpInfoColl = properties.UnicastAddresses;
            foreach (MockIPAddressInformation item in uniIpInfoColl)
            {
                if (item.Address.AddressFamily == family) 
                {
                    return true;
                }
            }
            return false;
          
        }

        public override bool SupportsMulticast
        {
            get { return false; }
        }
    }
    /// <summary>
    /// Mock VfInfo class to expose some encalpuslated functions
    /// </summary>
    public class MockVifInfo : VfInfo
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="exceptionhandler">refer to the wrapped function</param>
        /// <param name="session">refer to the wrapped function</param>
        public MockVifInfo(IExceptionHandler exceptionhandler, AWmiSession session)
            : base(exceptionhandler,session)
        {
        }
   
        /// <summary>
        /// Expose the getDeviceIndexFromIndexedDevicePath function
        /// </summary>
        /// <param name="indexedDevicePath">refer to the wrapped function</param>
        /// <returns>refer to the wrapped function</returns>
        public string accessGetDeviceIndexFromIndexedDevicePath(string indexedDevicePath) 
        {
            return base.getDeviceIndexFromIndexedDevicePath(indexedDevicePath);
        }
        /// <summary>
        /// Expose the findValidNic function
        /// </summary>
        /// <param name="mac">refer to the wrapped function</param>
        /// <param name="nics">refer to the wrapped function</param>
        /// <returns>refer to the wrapped function</returns>
        public NetworkInterface accessFindValidNic(string mac, NetworkInterface[] nics) 
        {
            return base.findValidNic(mac, nics);
        }
        /// <summary>
        /// Expose the updateNetworkInfo function
        /// </summary>
        /// <param name="nics">refer to the wrapped function</param>
        public void accessUpdateNetworkInfo(NetworkInterface[] nics) 
        {
             base.updateNetworkInfo(nics);
        }
    }
    /// <summary>
    /// Tester for VifInfo
    /// </summary>
    [TestClass]
    public class VifInfoTester
    {
        /// <summary>
        /// Mock session, mainly mock the xensore
        /// </summary>
        MockWmiSession session;
        /// <summary>
        /// VfInfo object to be tested
        /// </summary>
        MockVifInfo vf = null;
        /// <summary>
        /// Fake NICs 
        /// </summary>
        MockNetworkInterface[] nics;
        /// <summary>
        /// Fake ipv4, ipv6,mac info
        /// </summary>
        byte[] ipv4BytesNic1, ipv4BytesNic2;
        byte[] ipv6BytesNic1, ipv6BytesNic2;
        byte[] mac1, mac2;
       
        /// <summary>
        /// Get Ipv6 string from byte array
        /// </summary>
        /// <param name="ipv6byte">ipv6 address in bytes</param>
        /// <returns>ipv6 string seperated by ":"</returns>
        private string getIpv6String(byte[] ipv6byte) 
        {

            StringBuilder sb = new StringBuilder();
            const int IPV6_LEN = 8;

            for (int i = 0; i < IPV6_LEN; i++) 
            {
                string format = "{0:x}{1:x2}";
                if(i != IPV6_LEN-1)
                {
                    format += ":";
                }
                sb.AppendFormat(format, ipv6byte[2*i], ipv6byte[2*i+1]);
            }
            return sb.ToString();
        }
        /// <summary>
        /// Constructor, construct the VIfInfo object, NIC object, Session object
        /// </summary>
        public VifInfoTester()
        {
          //  arr = new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66 };
            mac1 = new byte[] { 0x32, 0x70, 0xd5, 0xa0, 0x5a, 0x21 };
            mac2 = new byte[] { 0x06, 0x49, 0xdf, 0xd0, 0xbb, 0x2b };

            session = new MockWmiSession();
            // Init mock session
            session.GetXenStoreItem("xenserver/device/net-sriov-vf", "");
            session.GetXenStoreItem("xenserver/device/net-sriov-vf/1", "");
            session.GetXenStoreItem("xenserver/device/net-sriov-vf/1/mac", UTFunctions.getMacString(mac1));
            session.GetXenStoreItem("xenserver/device/net-sriov-vf/2", "");
            session.GetXenStoreItem("xenserver/device/net-sriov-vf/2/mac", UTFunctions.getMacString(mac2));
            vf = new MockVifInfo(null, session);

            // Construct mock Nics info
            nics = new MockNetworkInterface[2];
            // Construct first Nic
            nics[0] = new MockNetworkInterface("Ethernet UT1", "This is mock NIC for UT1");
            ipv4BytesNic1 = new byte[] { 10, 71, 153, 8 };
            IPAddress addrIpv4 = new IPAddress(ipv4BytesNic1);
            ipv6BytesNic1 = new byte[] { 0xfd, 0x06, 0x77, 0x68, 0xb9, 0xe5, 0x8a, 0x40, 0x4, 0x49, 0xdf, 0xff, 0xfe, 0xd0, 0xbb, 0x2b };
            IPAddress addrIpv6 = new IPAddress(ipv6BytesNic1);
           
            PhysicalAddress physicalAddr = new PhysicalAddress(mac1);
            nics[0].SetPhysicalAddress(physicalAddr);
            

            MockUnicastIPAddressInformationCollection uniIPCol = new MockUnicastIPAddressInformationCollection();

            MockIPAddressInformation ipv4AddressInformation = new MockIPAddressInformation();
            ipv4AddressInformation.SetIPAddress(addrIpv4);

            MockIPAddressInformation ipv6AddressInformation = new MockIPAddressInformation();
            ipv6AddressInformation.SetIPAddress(addrIpv6);

            uniIPCol.Add(ipv4AddressInformation);
            uniIPCol.Add(ipv6AddressInformation);

            MockIPInterfaceProperties nicProperties = new MockIPInterfaceProperties(uniIPCol);
            nics[0].SetIPProperties(nicProperties);

            //Construct second Nic
            nics[1] = new MockNetworkInterface("Ethernet UT2", "This is mock NIC for UT2");
            ipv4BytesNic2 = new byte[] { 10, 71, 153, 9 };
            addrIpv4 = new IPAddress(ipv4BytesNic2);
            ipv6BytesNic2 = new byte[] { 0xfd, 0x06, 0x77, 0x68, 0xb9, 0xe5, 0x8a, 0x40, 0x04, 0x49, 0xdf, 0xff, 0xfe, 0xd0, 0xbb, 0xbb };
            addrIpv6 = new IPAddress(ipv6BytesNic2);

            uniIPCol = new MockUnicastIPAddressInformationCollection();

            ipv4AddressInformation = new MockIPAddressInformation();
            ipv4AddressInformation.SetIPAddress(addrIpv4);

            ipv6AddressInformation = new MockIPAddressInformation();
            ipv6AddressInformation.SetIPAddress(addrIpv6);

            uniIPCol.Add(ipv4AddressInformation);
            uniIPCol.Add(ipv6AddressInformation);

            nicProperties = new MockIPInterfaceProperties(uniIPCol);
            nics[1].SetIPProperties(nicProperties);
           
            physicalAddr = new PhysicalAddress(mac2);
            nics[1].SetPhysicalAddress(physicalAddr);

        }
       
       
        /// <summary>
        /// Test the getDeviceIndexFromIndexedDevicePath method
        /// </summary>
        [TestMethod]
        public void TestGetDeviceIndexFromIndexedDevicePath() 
        {
            string path = "xenserver/device/net-sriov-vf/2";
            string expectedDeviceId = "2";
            string deviceId = vf.accessGetDeviceIndexFromIndexedDevicePath(path);
            Assert.AreEqual(expectedDeviceId, expectedDeviceId);
        }
        /// <summary>
        /// Test the getAddrInfo method
        ///      1. Ipv4 address of the first nic should be found
        ///      2. Ipv6 address of the first nic should be found
        /// </summary>
        [TestMethod]
        public void TestGetAddrInfo() 
        {
            
            IEnumerable<IPAddress> addrs = vf.getAddrInfo(nics[0], AddressFamily.InterNetwork);
            bool result = true;
            foreach (var item in addrs)
            {
                byte[] ipByptes = item.GetAddressBytes();
                result &= ipByptes.SequenceEqual(ipv4BytesNic1);
            }
            Assert.IsTrue(result);
            addrs = vf.getAddrInfo(nics[0], AddressFamily.InterNetworkV6);
            foreach (var item in addrs)
            {
                byte[] ipByptes = item.GetAddressBytes();
                result &= ipByptes.SequenceEqual(ipv6BytesNic1);
            }
            Assert.IsTrue(result);
        }
        /// <summary>
        /// Test the findValidNic method by mac
        ///      1. the first NIC should be found by its mac address
        ///      2. providen other mac should not find any NIC object
        /// </summary>
        [TestMethod]
        public void TestFindValidNic() 
        {
            string macStr = UTFunctions.getMacString(mac1); ;
            NetworkInterface nic = vf.accessFindValidNic(macStr, nics);
            Assert.IsNotNull(nic);

            macStr = "06:49:df:d0:bb:5b";
            nic = vf.accessFindValidNic(macStr, nics);
            Assert.IsNull(nic);

        }
        /// <summary>
        /// Test the updateNetworkInfo method
        ///      1. the first nic info should be updated to xenserver/attr/net-sriov-vf/1
        ///      2. the second nic info should be updated to xenserver/attr/net-sriov-vf/2
        /// </summary>
        [TestMethod]
        public void TestUpdateNetworkInfo()
        {
            vf.accessUpdateNetworkInfo(nics);

            // Check Nic1 xenstore Nic info
            // Check Name
            MockXenStoreItem name = session.GetXenStoreItem("xenserver/attr/net-sriov-vf/1/name") as MockXenStoreItem ;
            Assert.AreEqual("Ethernet UT1", name.value);
            // Check Mac
            MockXenStoreItem mac = session.GetXenStoreItem("xenserver/attr/net-sriov-vf/1/mac") as MockXenStoreItem;
            Assert.AreEqual(UTFunctions.getMacString(mac1), mac.value);
            // Check ipv4
            MockXenStoreItem ipv4 = session.GetXenStoreItem("xenserver/attr/net-sriov-vf/1/ipv4/0") as MockXenStoreItem;
            string ipv4Expect = string.Format("{0:d}.{1:d}.{2:d}.{3:d}", ipv4BytesNic1[0], ipv4BytesNic1[1], ipv4BytesNic1[2],ipv4BytesNic1[3]);
            Assert.AreEqual(ipv4Expect, ipv4.value);
            // Check ipv6
            MockXenStoreItem ipv6 = session.GetXenStoreItem("xenserver/attr/net-sriov-vf/1/ipv6/0") as MockXenStoreItem;
            string expectedIpv6 = getIpv6String(ipv6BytesNic1);
            Assert.AreEqual(expectedIpv6, ipv6.value);

            // Check Nic2 xenstore Nic info
            // Check Name
            name = session.GetXenStoreItem("xenserver/attr/net-sriov-vf/2/name") as MockXenStoreItem;
            Assert.AreEqual("Ethernet UT2", name.value);
            // Check Mac
            mac = session.GetXenStoreItem("xenserver/attr/net-sriov-vf/2/mac") as MockXenStoreItem;
            Assert.AreEqual(UTFunctions.getMacString(mac2), mac.value);
            // Check ipv4
            ipv4 = session.GetXenStoreItem("xenserver/attr/net-sriov-vf/2/ipv4/0") as MockXenStoreItem;
            ipv4Expect = string.Format("{0:d}.{1:d}.{2:d}.{3:d}", ipv4BytesNic2[0], ipv4BytesNic2[1], ipv4BytesNic2[2], ipv4BytesNic2[3]);
            Assert.AreEqual(ipv4Expect, ipv4.value);
            // Check ipv6
            ipv6 = session.GetXenStoreItem("xenserver/attr/net-sriov-vf/2/ipv6/0") as MockXenStoreItem;
            expectedIpv6 = getIpv6String(ipv6BytesNic2);
            Assert.AreEqual(expectedIpv6, ipv6.value);
        }
    }
}
