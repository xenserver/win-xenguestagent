/* Copyright (c) Citrix Systems Inc.
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, 
 * with or without modification, are permitted provided 
 * that the following conditions are met:
 *
 * *   Redistributions of source code must retain the above 
 *     copyright notice, this list of conditions and the 
 *     following disclaimer.
 * *   Redistributions in binary form must reproduce the above 
 *     copyright notice, this list of conditions and the 
 *     following disclaimer in the documentation and/or other 
 *     materials provided with the distribution.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND 
 * CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, 
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF 
 * MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR 
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, 
 * BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR 
 * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, 
 * WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF 
 * SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Management;
using System.Diagnostics;
using System.ServiceProcess;
using Microsoft.Win32;
using System.Linq;

namespace xenwinsvc
{
    /// <summary>
    /// The abstract class define behavior of all network interface
    /// </summary>
    abstract public class NetInfo : IRefresh
    {

        /// <summary>
        /// The key to set the static ip
        /// </summary>
        protected const string STATIC_IP_FEATURE_MONITOR_KEY = "xenserver/device/vif";

        protected Object updating;
       
        /// <summary>
        /// The wmi session object, all sub-class needs to initialize this object
        /// </summary>
        protected WmiSession wmisession;

        /// <summary>
        /// update when catch new changes or refresh
        /// </summary>
        protected virtual void updateNicStatus()
        {
            updateNetworkInfo();
        }

        /// <summary>
        /// Refresh interval
        /// </summary>
        /// <param name="force">ignored</param>
        /// <returns>whether kickof the xenstore change</returns>
        public bool Refresh(bool force) 
        {
            try
            {
                updateNicStatus();
            }
            catch (System.Management.ManagementException x)
            {
                if (x.ErrorCode != ManagementStatus.AccessDenied)
                {
                    exceptionhandler.HandleException("Network Information", x);
                }
            }
            catch (Exception ex)
            {
                exceptionhandler.HandleException("Network Information", ex);
            }
            return true;
        }

        /// <summary>
        /// When catch the changes inside VM about the NIC
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void onVmNicAddrChange(Object sender, EventArgs e)
        {
            needsRefresh = true;
        }

        /// <summary>
        /// Xenstore item for the static ip setting
        /// </summary>
        XenStoreItem netStaticIpSetting;

        protected IExceptionHandler exceptionhandler;

        /// <summary>
        /// whether needs to refresh
        /// </summary>
        static protected bool needsRefresh = false;

        /// <summary>
        /// The devicePath monitor all the network devices, must be override by the sub-class
        /// The attrPath is the xenstore path where win-tools report network device configuration
        ///     must be override by the sub-class
        /// </summary>
        protected string devicePath, attrPath;
        /// <summary>
        /// The xenstore Item corresponding to devicePath and attrPath
        /// </summary>
        protected XenStoreItem netDeviceItem,netAttrItem;

        NetworkAddressChangedEventHandler addrChangeHandler;

        /// <summary>
        /// Constructor of the NetInfo class
        /// </summary>
        /// <param name="exceptionhandler">The exception handler, trigger when exception occurs</param>
        /// <param name="DEVICE_PATH"> xenstore device path of NIC device</param>
        /// <param name="ATTR_PATH"> xenstore path NIC device data info report back to</param>
        /// <param name="SESSION_NAME"> wmi session name for NIC device</param>
        public NetInfo(IExceptionHandler exceptionhandler,string devicePath,string attrPath, string sessionName)
        {
            this.exceptionhandler = exceptionhandler;
            updating = new Object();
            needsRefresh = true;

            wmisession = WmiBase.Singleton.GetXenStoreSession(sessionName);
            this.devicePath = devicePath;
            this.attrPath = attrPath;

            netDeviceItem = wmisession.GetXenStoreItem(devicePath);
            netAttrItem = wmisession.GetXenStoreItem(attrPath);

            netDeviceItem.Watch(onXenstoreNetChanged);

            addrChangeHandler = new NetworkAddressChangedEventHandler(onVmNicAddrChange);
            NetworkChange.NetworkAddressChanged += addrChangeHandler;

            netStaticIpSetting = wmisession.GetXenStoreItem(STATIC_IP_FEATURE_MONITOR_KEY);
            netStaticIpSetting.Watch(onXenstoreStaticIpSettingChanged);

            // trigger the first update
            needsRefresh = true;
        }

        /// <summary>
        /// Callback when device key changed
        /// </summary>
        /// <param name="nothing"></param>
        /// <param name="args"></param>
        void onXenstoreNetChanged(object nothing, EventArrivedEventArgs args)
        {
            needsRefresh = true;
        }

        /// <summary>
        /// Callback function when static ip setting changed
        /// </summary>
        /// <param name="nothing"></param>
        /// <param name="args"></param>
        protected virtual void onXenstoreStaticIpSettingChanged(object nothing, EventArrivedEventArgs args)
        {
            needsRefresh = true;
        }

        /// <summary>
        /// Set refresh flag
        /// </summary>
        protected virtual void RefreshNetInfo()
        {
            needsRefresh = true;
        }

        /// <summary>
        /// Whether needs to refresh
        /// </summary>
        /// <returns>whether refresh is needed</returns>
        virtual public bool NeedsRefresh()
        {
             return needsRefresh;
        }

        /// <summary>
        /// whether a nic macth with the providen mac address
        /// </summary>
        /// <param name="mac">mac address to be match</param>
        /// <param name="nic">the nic object representing a NIC inside VM</param>
        /// <returns></returns>
        virtual protected bool macsMatch(string mac, NetworkInterface nic)
        {
            byte[] macbytes = nic.GetPhysicalAddress().GetAddressBytes();
            if (macbytes.Length != 6) // Looks like an Ethernet mac address 
            {
                Debug.Print("Attempting to match non-ethernet physical address");
                return false;
            }
            
            string macmatchstr = macbytes[0].ToString("x2") + ":" +
                                         macbytes[1].ToString("x2") + ":" +
                                         macbytes[2].ToString("x2") + ":" +
                                         macbytes[3].ToString("x2") + ":" +
                                         macbytes[4].ToString("x2") + ":" +
                                         macbytes[5].ToString("x2");
            Debug.Print("Matching \"" + macmatchstr + "\" and \"" + mac.ToLower() + "\"");

            return (macmatchstr.Equals(mac.ToLower()));

        }
        /// <summary>
        /// Get an IP address info of an NIC, for specific address family
        /// </summary>
        /// <param name="nic">the nic object representing a NIC inside VM</param>
        /// <param name="addressFamily">ipv4 or ipv6</param>
        /// <returns></returns>
        virtual protected IEnumerable<IPAddress> getAddrInfo(NetworkInterface nic, System.Net.Sockets.AddressFamily addressFamily)
        {

            if (nic.Supports(NetworkInterfaceComponent.IPv6) || nic.Supports(NetworkInterfaceComponent.IPv4))
            {
                IPInterfaceProperties ipprop = nic.GetIPProperties();
                foreach (UnicastIPAddressInformation addr in ipprop.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == addressFamily)
                    {
                        yield return addr.Address;
                    }
                }
            }
        }
        /// <summary>
        /// Clear the reported NIC attr info
        /// </summary>
        virtual protected void removeNicAttr()
        {
            try
            {

                foreach (string node in netAttrItem.children)
                {
                    try
                    {
                        wmisession.GetXenStoreItem(node).Remove();
                    }
                    catch { }; // Ignore failure, if nodes don't exist
                }
            }
            catch { }; // If there are no nodes, then we can also ignore failure
        }
        /// <summary>
        /// Update all the devices info into xensore
        /// </summary>
        /// <param name="devices">The devices key from xenstore, representing devices from xenopsd, needs to be udpated</param>
        /// <param name="nics">the nic objects representing a NIC inside VM</param>
        virtual protected void updateNicAttr(string[] devices, NetworkInterface[] nics) 
        {
            foreach (var device in devices)
            {
                XenStoreItem macItem = wmisession.GetXenStoreItem(device + "/mac");
                if (!macItem.Exists() || "".Equals(macItem.value))
                {
                    Debug.Print("Warning: xenstored should provide mac address for this vf device");
                    Debug.Print("Warning: ignore device {0}", device);
                    continue;
                }

                string mac = macItem.value;

                NetworkInterface nic = findValidNic(mac, nics);
                if (null != nic)
                {
                    writeDevice(device, nic);
                }
                else 
                {
                    Debug.Print("does not find nic for mac: " + mac);
                }
               
            }
        }
        /// <summary>
        /// Update the device info into into xenstore
        /// </summary>
        /// <param name="device">The device key from xenstore, representing device from xenopsd, needs to be udpated</param>
        /// <param name="nic">the nic object representing a NIC inside VM</param>
        abstract protected void writeDevice(string device, NetworkInterface nic);
        /// <summary>
        /// Get the device id from the device path
        /// </summary>
        /// <param name="indexedDevicePath">like: ~xenserver/device/net-sriov-vf/2</param>
        /// <returns></returns>
        public string getDeviceIndexFromIndexedDevicePath(string indexedDevicePath)
        {
            if (!indexedDevicePath.Contains(devicePath) || indexedDevicePath.Length <= devicePath.Length) 
            {
                throw new Exception(string.Format("Invalid indexedDevicePath: {0}",indexedDevicePath));
            }
            return indexedDevicePath.Substring(devicePath.Length + 1);
        }
        /// <summary>
        /// Find valid nic object from the nics array, accroding to the mac address
        /// </summary>
        /// <param name="mac">mac string to identify the nic</param>
        /// <param name="nics">all the nic objects inside the VM</param>
        /// <returns></returns>
        virtual protected NetworkInterface findValidNic(string mac, NetworkInterface[] nics)
        {
            if(null == mac || nics == null)
            {
                Debug.Print("invalid parameter for findValidNic");
                return null;
            }
            NetworkInterface[] validNics = (from nic in nics where macsMatch(mac, nic) select nic).ToArray<NetworkInterface>();
            if (null == validNics || 0 == validNics.Length)
            {
                Debug.Print("does not find valid nic for mac: " + mac);
                return null;
            }
            else 
            {
                return validNics[0];
            }
        }
        /// <summary>
        /// Update xenstore related Netork info
        /// </summary>
        private void updateNetworkInfo()
        {
            lock (updating)
            {
                needsRefresh = false;
                removeNicAttr();
                string[] devices;
                NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
                try
                {
                    devices = netDeviceItem.children;
                }
                catch
                {
                    return; //No children means no devices to store data about
                }
                wmisession.StartTransaction();

                try
                {
                    try
                    {
                        updateNicAttr(devices, nics);
                    }
                    catch (Exception)
                    {
                        wmisession.AbortTransaction();
                        throw;
                    }
                    wmisession.CommitTransaction();


                }
                catch
                {
                    needsRefresh = true;
                };

            }
        }
        /// <summary>
        /// Remove the Network callback function
        /// </summary>
        protected virtual void Finish()
        {
            NetworkChange.NetworkAddressChanged -= addrChangeHandler;
        }

        bool disposed = false;
        /// <summary>
        /// Disposing the object
        /// </summary>
        /// <param name="disposing">whether disposing from Dispose method</param>
        void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    Finish();
                }
            }
            disposed = true;
        }
        /// <summary>
        /// Dispose and block the GC
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        /// <summary>
        /// Destructor
        /// </summary>
        ~NetInfo()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// Class for the PV network
    /// </summary>
    public class VifInfo : NetInfo {

        XenStoreItem numvif;
        const string DEVICE_PATH = "device/vif";
        const string ATTR_PATH  = "data/vif";
        const string SESSION_NAME = "Adapters";

        /// <summary>
        /// PV NIC constructor
        /// </summary>
        /// <param name="exceptionhandler"></param>
        /// <param name="DEVICE_PATH"> xenstore device path of pv device</param>
        /// <param name="ATTR_PATH"> xenstore path pv device data info report back to</param>
        /// <param name="SESSION_NAME"> wmi session name for pv device</param>
        public VifInfo(IExceptionHandler exceptionhandler)
            : base(exceptionhandler, DEVICE_PATH, ATTR_PATH, SESSION_NAME)
        {
         
            numvif = wmisession.GetXenStoreItem("data/num_vif");           
        }

       /// <summary>
       /// Override the parent method, also update the number of devices into xenstore
       /// </summary>
       /// <param name="devices">Device keys from device path</param>
       /// <param name="nics">All the nics inside VM</param>
       override protected void updateNicAttr(string[] devices, NetworkInterface[] nics) 
        {
            numvif.value = devices.Length.ToString();
            base.updateNicAttr(devices,nics);
        }
        /// <summary>
        /// Write Device into xenstore, only update for the first time
        /// </summary>
        /// <param name="device">device key representing a device</param>
        /// <param name="nic">nic object inside VM</param>
        override protected void writeDevice(string device, NetworkInterface nic)
        {
         
            string namePath = string.Format("{0}/{1}/name", attrPath, getDeviceIndexFromIndexedDevicePath(device));
            XenStoreItem name = wmisession.GetXenStoreItem(namePath);
            name.value = nic.Name;
            if (name.GetStatus() != ManagementStatus.NoError)
            {
                Debug.Print(string.Format("write to {0} error",namePath));
            }
        
        }
        /// <summary>
        /// Overwrite the to also save chagned network setting
        /// </summary>
        override  protected void updateNicStatus() 
        {
            base.updateNicStatus();
            StoreChangedNetworkSettings();
        }


        const string NETSETTINGSSTORE = @"SOFTWARE\Citrix\XenToolsNetSettings";
        const string ENUM = @"SYSTEM\CurrentControlSet\Enum\";
        const string CONTROL = @"SYSTEM\CurrentControlSet\Control\";
        const string CLASS = CONTROL + @"Class\";
        const string NETWORKUUID = @"{4D36E972-E325-11CE-BFC1-08002BE10318}";
        const string NETWORKDEVICE = CLASS + NETWORKUUID + @"\";
        const string TCPIPSRV = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";
        const string TCPIP6SRV = @"SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters\Interfaces";
        const string NETBTSRV = @"SYSTEM\CurrentControlSet\Services\NetBT\Parameters\Interfaces";
        const string NSI = CLASS + @"NSI\";
        const string STORE = @"SOFTWARE\Citrix\XenToolsNetSettings\";
        const string STATICIPV4STORE = STORE + @"IPV4";
        const string STATICIPV6STORE = STORE + @"IPV6";
        const string STATICIPV4 = NSI + @"{eb004a00-9b1a-11d4-9123-0050047759bc}\10\";
        const string STATICIPV6 = NSI + @"{eb004a01-9b1a-11d4-9123-0050047759bc}\10\";

        static string FindClassDeviceKeyNameForNetCfgInstanceId(string NetCfgInstanceId)
        {
            using (RegistryKey netdevkey = Registry.LocalMachine.OpenSubKey(NETWORKDEVICE))
            {
                foreach (string keyname in netdevkey.GetSubKeyNames())
                {
                    try
                    {
                        using (RegistryKey devicekey = netdevkey.OpenSubKey(keyname))
                        {
                            if (((string)devicekey.GetValue("NetCfgInstanceId")).Equals(NetCfgInstanceId))
                            {
                                return keyname;
                            }
                        }
                    }
                    catch { }
                }
            }
            throw new Exception("Unable to find Class Device Key Name for NefCfgInstance");

        }

        static RegistryKey FindClassDeviceKeyForNetCfgInstanceId(string NetCfgInstanceId)
        {
            using (RegistryKey netdevkey = Registry.LocalMachine.OpenSubKey(NETWORKDEVICE))
            {
                RegistryKey devicekey = netdevkey.OpenSubKey(FindClassDeviceKeyNameForNetCfgInstanceId(NetCfgInstanceId));
                if (((string)devicekey.GetValue("NetCfgInstanceId")).Equals(NetCfgInstanceId))
                {
                    return devicekey;
                }
                else
                {
                    devicekey.Close();
                }
            }
            throw new Exception("Unable to find Class Device Key for Mac");
        }

        static string FindClassDeviceNameForNetCfgInstanceId(string NetCfgInstanceId)
        {
            return NETWORKUUID + @"\" + FindClassDeviceKeyNameForNetCfgInstanceId(NetCfgInstanceId);
        }

        static string GetMacStrFromPhysical(PhysicalAddress pa)
        {
            byte[] macbyte = pa.GetAddressBytes();
            string pastr = string.Format("{0:X2}{1:X2}{2:X2}{3:X2}{4:X2}{5:X2}", macbyte[0], macbyte[1], macbyte[2], macbyte[3], macbyte[4], macbyte[5]);
            return pastr;
        }

        static string FindNetCfgInstanceIdForMac(string mac)
        {
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface nic in nics)
            {
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                {
                    if (nic.OperationalStatus == OperationalStatus.Up)
                    {
                        string NetCfgInstanceId = nic.Id;
                        string pastr = GetMacStrFromPhysical(nic.GetPhysicalAddress());
                        if (pastr.Equals(mac))
                        {
                            return NetCfgInstanceId;
                        }
                    }
                }
            }
            throw new Exception("Unable to find NetCfgInstanceId for mac address");
        }

        static string FindDeviceKeyForMac(string mac)
        {
            string NetCfgInstanceId = FindNetCfgInstanceIdForMac(mac);

            using (RegistryKey devicekey = FindClassDeviceKeyForNetCfgInstanceId(NetCfgInstanceId))
            {
                return (string)devicekey.GetValue("DeviceInstanceId");
            }
        }

        static string FindNetLuidMatchStrForNetCfgInstanceId(string NetCfgInstanceId)
        {
            using (RegistryKey classdevkey = FindClassDeviceKeyForNetCfgInstanceId(NetCfgInstanceId))
            {
                int LuidIndex = (int)classdevkey.GetValue("NetLuidIndex");
                Debug.Print("LuidIndex " + LuidIndex.ToString());
                int IfType = (int)classdevkey.GetValue("*IfType");
                Debug.Print("*IfType " + IfType.ToString());
                string LuidStr = new string(string.Format("{0:x6}", LuidIndex & 0xFFFFFF).ToCharArray().Reverse().ToArray());
                string IfStr = new string(string.Format("{0:x4}", IfType & 0xFFFF).ToCharArray().Reverse().ToArray());
                string matchstr = string.Format("000000" + LuidStr + IfStr);
                Debug.Print("Match String " + matchstr);
                return matchstr;
            }
        }

        static string FindNetCfgInstanceIdForDriverKey(string driverkey)
        {
            Debug.Print("Driverkey " + driverkey);
            using (RegistryKey enumkey = Registry.LocalMachine.OpenSubKey(ENUM + driverkey))
            {
                string classkeyname = (string)enumkey.GetValue("Driver");
                Debug.Print("Class key name " + classkeyname);
                using (RegistryKey classkey = Registry.LocalMachine.OpenSubKey(CLASS + classkeyname))
                {
                    string NetCfgInstanceId = (string)classkey.GetValue("NetCfgInstanceId");
                    return NetCfgInstanceId;
                }
            }
        }

        public static void RecordDevices(string type)
        {
            Trace.WriteLine("NETINFO Record " + type);
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface nic in nics)
            {
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                {
                    PhysicalAddress pa = nic.GetPhysicalAddress();
                    string NetCfgInstanceId = nic.Id;
                    Trace.WriteLine("ID = " + NetCfgInstanceId);
                    try
                    {
                        using (RegistryKey netsetstorekey = Registry.LocalMachine.CreateSubKey(NETSETTINGSSTORE))
                        {
                            using (RegistryKey emulatedkey = netsetstorekey.CreateSubKey(type))
                            {
                                string classname = FindClassDeviceNameForNetCfgInstanceId(NetCfgInstanceId);

                                using (RegistryKey enumkey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum"))
                                {
                                    foreach (string bus in enumkey.GetSubKeyNames())
                                        using (RegistryKey buskey = enumkey.OpenSubKey(bus))
                                        {
                                            foreach (string busdriver in buskey.GetSubKeyNames())
                                                using (RegistryKey busdriverkey = buskey.OpenSubKey(busdriver))
                                                {
                                                    foreach (string busdriverdevice in busdriverkey.GetSubKeyNames())
                                                        using (RegistryKey busdriverdevicekey = busdriverkey.OpenSubKey(busdriverdevice))
                                                        {
                                                            try
                                                            {
                                                                string driver = (string)busdriverdevicekey.GetValue("Driver");
                                                                if (driver.Equals(classname, StringComparison.InvariantCultureIgnoreCase))
                                                                {
                                                                    Trace.WriteLine("NETINFO Record " + pa.ToString());
                                                                    emulatedkey.SetValue(GetMacStrFromPhysical(pa), bus + "\\" + busdriver + "\\" + busdriverdevice);
                                                                }
                                                            }
                                                            catch
                                                            {
                                                            }
                                                        }
                                                }
                                        }
                                }


                                /*Trace.WriteLine("Class device device " + classname);
                                using (RegistryKey classkey  = Registry.LocalMachine.OpenSubKey(CLASS+classname)){
                                    string devname = (string)classkey.GetValue("MatchingDeviceId");
                                    Trace.WriteLine("Found device " + devname);
                                    using (RegistryKey devicekey = Registry.LocalMachine.OpenSubKey(ENUM+devname))
                                    {
                                        Trace.WriteLine("Opened");
                                        foreach (string id in devicekey.GetSubKeyNames())
                                        {
                                            Trace.WriteLine("checkid " + id);
                                            using (RegistryKey instancekey = devicekey.OpenSubKey(id))
                                            {
                                                string driver = (string)instancekey.GetValue("Driver");
                                                Trace.WriteLine("Check driver" + driver);
                                                if (driver.Equals(classname,StringComparison.InvariantCultureIgnoreCase))
                                                {
                                                    Trace.WriteLine("NETINFO Record " + pa.ToString());
                                                    emulatedkey.SetValue(GetMacStrFromPhysical(pa), devname+"\\"+id);
                                                }
                                            }
                                        }
                                    }
                                }*/
                            }
                        }
                    }
                    catch
                    {
                        Trace.WriteLine("No stored settings");
                    }
                }
            }
        }

        private static void ClonePVStatics(string SrcName, string DestName, string SrcNetLuidMatchStr, string DestNetLuidMatchStr, bool delete)
        {
            try
            {
                using (RegistryKey staticstore = Registry.LocalMachine.OpenSubKey(SrcName, true))
                using (RegistryKey netstatic = Registry.LocalMachine.CreateSubKey(DestName))
                {
                    if (staticstore.GetValueNames().Contains(SrcNetLuidMatchStr))
                    {
                        netstatic.SetValue(DestNetLuidMatchStr, staticstore.GetValue(SrcNetLuidMatchStr),
                            staticstore.GetValueKind(SrcNetLuidMatchStr));
                        if (delete)
                        {
                            staticstore.DeleteValue(SrcNetLuidMatchStr);
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static void CloneValues(RegistryKey src, RegistryKey dest)
        {
            string[] valuenames = src.GetValueNames();
            foreach (string name in valuenames)
            {
                dest.SetValue(name, src.GetValue(name), src.GetValueKind(name));
            }
        }

        private static void FromServiceIfaceToStore(string Src, RegistryKey StoreKey)
        {
            Debug.Print("do tcpip");
            using (RegistryKey tcpipsrckey = Registry.LocalMachine.OpenSubKey(TCPIPSRV).OpenSubKey(Src))
            {
                using (RegistryKey tcpipdestkey = StoreKey.CreateSubKey("Tcpip"))
                {
                    CloneValues(tcpipsrckey, tcpipdestkey);
                }
            }
            Debug.Print("do tcpip6");
            using (RegistryKey tcpipsrckey = Registry.LocalMachine.OpenSubKey(TCPIP6SRV).OpenSubKey(Src))
            {
                using (RegistryKey tcpipdestkey = StoreKey.CreateSubKey("Tcpip6"))
                {
                    CloneValues(tcpipsrckey, tcpipdestkey);
                }
            }
            Debug.Print("do netbt");
            using (RegistryKey tcpipsrckey = Registry.LocalMachine.OpenSubKey(NETBTSRV).OpenSubKey("Tcpip_" + Src))
            {
                using (RegistryKey tcpipdestkey = StoreKey.CreateSubKey("NetBT"))
                {
                    CloneValues(tcpipsrckey, tcpipdestkey);
                }
            }
        }

        private static void FromServiceIfaceToSeviceIface(string Src, string Dest)
        {
            using (RegistryKey tcpipsrckey = Registry.LocalMachine.OpenSubKey(TCPIPSRV).OpenSubKey(Src))
            {
                using (RegistryKey tcpipdestkey = Registry.LocalMachine.OpenSubKey(TCPIPSRV).OpenSubKey(Dest, true))
                {
                    CloneValues(tcpipsrckey, tcpipdestkey);
                }
            }

            using (RegistryKey tcpipsrckey = Registry.LocalMachine.OpenSubKey(TCPIP6SRV).OpenSubKey(Src))
            {
                using (RegistryKey tcpipdestkey = Registry.LocalMachine.OpenSubKey(TCPIP6SRV).OpenSubKey(Dest, true))
                {
                    CloneValues(tcpipsrckey, tcpipdestkey);
                }
            }

            using (RegistryKey tcpipsrckey = Registry.LocalMachine.OpenSubKey(NETBTSRV).OpenSubKey("Tcpip_" + Src))
            {
                using (RegistryKey tcpipdestkey = Registry.LocalMachine.OpenSubKey(NETBTSRV).OpenSubKey("Tcpip_" + Dest, true))
                {
                    CloneValues(tcpipsrckey, tcpipdestkey);
                }
            }
        }

        private static void FromStoreToServiceIface(RegistryKey StoreKey, string Dest)
        {
            using (RegistryKey tcpipsrckey = StoreKey.OpenSubKey("Tcpip"))
            {
                using (RegistryKey tcpipdestkey = Registry.LocalMachine.OpenSubKey(TCPIPSRV).CreateSubKey(Dest))
                {
                    CloneValues(tcpipsrckey, tcpipdestkey);
                }
            }

            using (RegistryKey tcpipsrckey = StoreKey.OpenSubKey("Tcpip6"))
            {
                using (RegistryKey tcpipdestkey = Registry.LocalMachine.OpenSubKey(TCPIP6SRV).CreateSubKey(Dest))
                {
                    CloneValues(tcpipsrckey, tcpipdestkey);
                }
            }

            using (RegistryKey tcpipsrckey = StoreKey.OpenSubKey("NetBT"))
            {
                using (RegistryKey tcpipdestkey = Registry.LocalMachine.OpenSubKey(NETBTSRV).CreateSubKey("Tcpip_" + Dest))
                {
                    CloneValues(tcpipsrckey, tcpipdestkey);
                }
            }
        }

        private static void StorePVNetworkSettingsToEmulatedDevicesOrSave()
        {
            Trace.WriteLine("NETINFO StorePVNetworkSettingsToEmulatedDevicesOrSave");
            using (RegistryKey emustore = Registry.LocalMachine.CreateSubKey(NETSETTINGSSTORE + @"\Emulated"))
            using (RegistryKey store = Registry.LocalMachine.CreateSubKey(NETSETTINGSSTORE))
            using (RegistryKey pvstore = Registry.LocalMachine.CreateSubKey(NETSETTINGSSTORE + @"\PV"))
            {
                try
                {
                    if (((String)store.GetValue("Status")).Equals("DontUpdate"))
                    {
                        Trace.WriteLine("Do not update stored values");
                        return;
                    }
                }
                catch { }

                NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
                foreach (NetworkInterface nic in nics)
                {
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                    {
                        PhysicalAddress pa = nic.GetPhysicalAddress();
                        string matchmac = GetMacStrFromPhysical(pa);
                        if (emustore.GetValueNames().Contains(matchmac))
                        {
                            if (pvstore.GetValueNames().Contains(matchmac))
                            {
                                Trace.WriteLine("NETINFO Match " + pa.ToString());
                                string SrcNetCfgInstanceId;
                                string DestNetCfgInstanceId;
                                string DestNetLuidMatchStr;
                                string SrcNetLuidMatchStr;
                                try
                                {
                                    SrcNetCfgInstanceId = FindNetCfgInstanceIdForDriverKey((string)pvstore.GetValue(matchmac));
                                    DestNetCfgInstanceId = FindNetCfgInstanceIdForDriverKey((string)emustore.GetValue(matchmac));
                                }
                                catch
                                {
                                    continue;
                                }
                                FromServiceIfaceToSeviceIface(SrcNetCfgInstanceId, DestNetCfgInstanceId);
                                try
                                {
                                    DestNetLuidMatchStr = FindNetLuidMatchStrForNetCfgInstanceId(DestNetCfgInstanceId);
                                    SrcNetLuidMatchStr = FindNetLuidMatchStrForNetCfgInstanceId(SrcNetCfgInstanceId);
                                }
                                catch
                                {
                                    continue;
                                }
                                ClonePVStatics(STATICIPV4, STATICIPV4, SrcNetLuidMatchStr, DestNetLuidMatchStr, false);
                                ClonePVStatics(STATICIPV6, STATICIPV6, SrcNetLuidMatchStr, DestNetLuidMatchStr, false);
                            }
                        }
                        else
                        {

                            Debug.Print("MAC " + matchmac);
                            Trace.WriteLine("NETINFO Save " + pa.ToString());
                            if (pvstore.GetValueNames().Contains(matchmac))
                            {
                                string SrcNetLuidMatchStr;
                                Debug.Print("Got");
                                string SrcNetCfgInstanceId = FindNetCfgInstanceIdForDriverKey((string)pvstore.GetValue(matchmac));
                                Debug.Print("NetCfg" + SrcNetCfgInstanceId);
                                using (RegistryKey macstore = Registry.LocalMachine.CreateSubKey(NETSETTINGSSTORE + @"\Mac"))
                                {
                                    Debug.Print("Found mac");
                                    using (RegistryKey ifacekey = macstore.CreateSubKey(matchmac))
                                    {
                                        //We only want to save over the top of entries we believe to
                                        //be PV, to avoid overwriting emulated entries when a new
                                        //PV device is installed.
                                        string ifacetype = (string)ifacekey.GetValue("ifacetype", "NONE");
                                        Trace.WriteLine(matchmac + " ifacetype = " + ifacetype);
                                        if (!ifacetype.Equals("Emulated"))
                                        {
                                            Debug.Print("Create mac");
                                            ifacekey.SetValue("ifacetype", "PV", RegistryValueKind.String);
                                            FromServiceIfaceToStore(SrcNetCfgInstanceId, ifacekey);
                                        }
                                        else
                                        {
                                            Trace.WriteLine("Don't overwrite emulated");
                                            continue;
                                        }
                                    }
                                }
                                try
                                {
                                    SrcNetLuidMatchStr = FindNetLuidMatchStrForNetCfgInstanceId(SrcNetCfgInstanceId);
                                }
                                catch
                                {
                                    continue;
                                }
                                ClonePVStatics(STATICIPV4, STATICIPV4STORE, SrcNetLuidMatchStr, SrcNetLuidMatchStr, false);
                                ClonePVStatics(STATICIPV6, STATICIPV6STORE, SrcNetLuidMatchStr, SrcNetLuidMatchStr, false);
                            }
                        }
                    }
                }
            }
        }

        private static void StoreSavedNetworkSettingsToEmulatedDevices()
        {
            Trace.WriteLine("NETINFO StoreSavedNetworkSettingsToEmulatedDevices");
            using (RegistryKey macstore = Registry.LocalMachine.CreateSubKey(NETSETTINGSSTORE + @"\Mac"))
            using (RegistryKey emustore = Registry.LocalMachine.CreateSubKey(NETSETTINGSSTORE + @"\Emulated"))
            {
                foreach (string macaddr in macstore.GetSubKeyNames())
                {

                    try
                    {
                        using (RegistryKey ifacekey = macstore.OpenSubKey(macaddr, true))
                        {
                            if (((string)ifacekey.GetValue("ifacetype")).Equals("PV"))
                            {
                                string NetCfgInstanceId;
                                try
                                {
                                    NetCfgInstanceId = FindNetCfgInstanceIdForDriverKey((string)emustore.GetValue(macaddr));
                                }
                                catch
                                {
                                    continue;
                                }
                                Trace.WriteLine("NETINFO Store " + macaddr);
                                FromStoreToServiceIface(ifacekey, NetCfgInstanceId);
                                ifacekey.SetValue("ifacetype", "Emulated", RegistryValueKind.String);
                                string ifacetype = (string)ifacekey.GetValue("ifacetype", "NONE");
                                Trace.WriteLine("ifacetype = " + ifacetype);

                            }
                            else
                            {
                                continue;
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }

            using (RegistryKey pvstore = Registry.LocalMachine.CreateSubKey(NETSETTINGSSTORE + @"\PV"))
            using (RegistryKey emustore = Registry.LocalMachine.CreateSubKey(NETSETTINGSSTORE + @"\Emulated"))
            {
                foreach (string devmac in pvstore.GetValueNames())
                {
                    if (emustore.GetValueNames().Contains(devmac))
                    {
                        string EmuNetCfgInstanceId = FindNetCfgInstanceIdForDriverKey((string)emustore.GetValue(devmac));
                        string PVNetCfgInstanceId = FindNetCfgInstanceIdForDriverKey((string)pvstore.GetValue(devmac));
                        string EmuNetLuidMatchStr = FindNetLuidMatchStrForNetCfgInstanceId(EmuNetCfgInstanceId);
                        string PVNetLuidMatchStr = FindNetLuidMatchStrForNetCfgInstanceId(PVNetCfgInstanceId);

                        ClonePVStatics(STATICIPV4STORE, STATICIPV4, PVNetLuidMatchStr, EmuNetLuidMatchStr, true);
                        ClonePVStatics(STATICIPV6STORE, STATICIPV6, PVNetLuidMatchStr, EmuNetLuidMatchStr, true);
                    }
                }
            }

        }

        public static void StoreChangedNetworkSettings()
        {
            try
            {
                ServiceController sc = null;
                ServiceControllerStatus status = ServiceControllerStatus.Stopped;
                try
                {
                    sc = new ServiceController("XenNet");
                    status = sc.Status;
                }
                catch
                {
                    sc = null;
                }

                if ((sc != null) && (status == ServiceControllerStatus.Running))
                {
                    RecordDevices("PV");
                    StorePVNetworkSettingsToEmulatedDevicesOrSave();
                }
                else
                {
                    Debug.Print("No xennet found");
                    try
                    {
                        RecordDevices("Emulated");
                        StoreSavedNetworkSettingsToEmulatedDevices();
                    }
                    catch (Exception e)
                    {
                        Debug.Print("Store changed settings " + e.ToString());
                    }
                }

            }
            catch (Exception e)
            {
                Debug.Print(e.ToString());
                throw;
            }
        }
    }

    /// <summary>
    /// The VF for SR-IOV NICs
    /// </summary>
    public class VfInfo:NetInfo
    {
        /// <summary>
        /// Device path for SR-IOV
        /// </summary>
        const string DEVICE_PATH = "xenserver/device/net-sriov-vf";
        /// <summary>
        /// The xenstore path win-tools will report back to
        /// </summary>
        const string ATTR_PATH = "xenserver/attr/net-sriov-vf";
        /// <summary>
        /// WMI session name
        /// </summary>
        const string SESSION_NAME = "SriovAdapters";

        /// <summary>
        ///  The constructor, build up wmisession and watch xenstore keys
        /// </summary>
        /// <param name="exceptionhandler"> exceptionhandler passed to base class</param>
        /// <param name="DEVICE_PATH"> xenstore device path of sriov</param>
        /// <param name="ATTR_PATH"> xenstore path sriov data info report back to</param>
        /// /// <param name="SESSION_NAME"> wmi session name for sriov device</param>
        public VfInfo(IExceptionHandler exceptionhandler):
            base(exceptionhandler,DEVICE_PATH,ATTR_PATH,SESSION_NAME)
        {
           
        }

        /// <summary>
        /// Override method to customize how to report the NIC data
        /// </summary>
        /// <param name="device">The device needs to be reported</param>
        /// <param name="nic">nic object inside VM</param>
        override protected void writeDevice(string device, NetworkInterface nic)
        {
            if (null == device || null == nic) return; // We trust xenstore, it is a SR-IOV device
            string deviceId = getDeviceIndexFromIndexedDevicePath(device);
            string nameKey = String.Format("{0}/{1}/name", attrPath, deviceId);
            string macKey = String.Format("{0}/{1}/mac", attrPath, deviceId);

            // Update the xenstore info
            try
            {
                // Update name
                XenStoreItem xenName = wmisession.GetXenStoreItem(nameKey);
                xenName.value = nic.Name;

                // Update mac
                XenStoreItem xenMac = wmisession.GetXenStoreItem(macKey);
                xenMac.value = nic.GetPhysicalAddress().ToString();

                // Update ipv4 info
                updateVFIpInfo(deviceId, System.Net.Sockets.AddressFamily.InterNetwork, nic);

                // Update the ipv6 info
                updateVFIpInfo(deviceId, System.Net.Sockets.AddressFamily.InterNetworkV6, nic);

            }
            catch (Exception e)
            {
                Debug.Print("updateVFXenstoreAttrInfo error: {0}", e);
            }

        }
   
        /// <summary>
        /// Update device ip address info into xenstore
        /// </summary>
        /// <param name="deviceId">current device id, specified by xenposd</param>
        /// <param name="addressFamily">System.Net.Sockets.AddressFamily.InterNetwork for ipv4, InterNetworkV6 for ipv6</param>
        /// <param name="nic">current nic info which will be updated into xenstore</param>
        private void updateVFIpInfo(string deviceId, System.Net.Sockets.AddressFamily addressFamily, NetworkInterface nic)
        {
            // Clean the item to avoid zombie record
            string ipKind = addressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? "ipv4" : "ipv6";
        
            // Fresh the VF info into database
            int index = 0;
            foreach (var item in getAddrInfo(nic, addressFamily))
            {
                string ipAddrKey = String.Format("{0}/{1}/{2}/{3}", attrPath, deviceId, ipKind,index++);
                XenStoreItem ipAddrItem = wmisession.GetXenStoreItem(ipAddrKey);
                ipAddrItem.value = item.ToString();
            }
        }

    }

}
