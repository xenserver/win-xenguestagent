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

    public class NetInfo : IRefresh
    {

        Object updating;
        IExceptionHandler exceptionhandler;
        WmiSession wmisession;
        XenStoreItem devicevif;
        XenStoreItem datavif;
        XenStoreItem numvif;
        XenStoreItem vifStaticIpSetting;
        const string vifpath = "device/vif";
        static public void RefreshNetInfo()
        {
            needsRefresh = true;
        }
        static private bool needsRefresh = false;

        public bool NeedsRefresh()
        {
             return needsRefresh;
        }

        bool macsMatch(string mac, NetworkInterface nic)
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

        string getIpv4AddrString(NetworkInterface nic)
        {
            if (nic.Supports(NetworkInterfaceComponent.IPv4))
            {
                IPInterfaceProperties ipprop = nic.GetIPProperties();
                IPv4InterfaceProperties ipv4prop = ipprop.GetIPv4Properties();
                foreach (UnicastIPAddressInformation addr in ipprop.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return addr.Address.ToString();
                    }
                }
            }
            return null;
        }
        

        IEnumerable<IPAddress> getIpv6Addr(NetworkInterface nic)
        {
            if (nic.Supports(NetworkInterfaceComponent.IPv6))
            {
                IPInterfaceProperties ipprop = nic.GetIPProperties();
                IPv6InterfaceProperties ipv6prop = ipprop.GetIPv6Properties();
                foreach (UnicastIPAddressInformation addr in ipprop.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                    {
                        yield return addr.Address;
                    }
                }
            }
        }


 
        void removeDevices()
        {
            try
            {

                foreach (string node in datavif.children)
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

        bool enablewritedevice = true;
        void writeDevice(string device, NetworkInterface[] nics)
        {
            if (enablewritedevice) {
                string mac = wmisession.GetXenStoreItem(device + "/mac").value;
                foreach (NetworkInterface nic in nics)
                {
                    if (macsMatch(mac, nic))
                    {
                        XenStoreItem name = wmisession.GetXenStoreItem("data/vif/" + device.Substring(vifpath.Length + 1) + "/name");
                        name.value = nic.Name;
                        if (name.GetStatus() != ManagementStatus.NoError) {
                            enablewritedevice = false;
                            return;
                        }
                    }
                }
            }
        }


        private void updateNetworkInfo()
        {
            lock(updating) {
                needsRefresh = false;
                removeDevices();
                string[] devices;
                NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
                try
                {
                    devices = devicevif.children;
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
                        numvif.value = devices.Length.ToString();
                        foreach (string device in devices)
                        {
                            writeDevice(device, nics);
                        }
                    }
                    catch (Exception){
                        wmisession.AbortTransaction();
                        throw;
                    }
                    wmisession.CommitTransaction();
                    
     
                }
                catch {
                    needsRefresh = true; 
                };
                
            }
        }

        public bool Refresh(bool force)
        {
            updateNetworkInfo();
            NetInfo.StoreChangedNetworkSettings();
            return true;
        }

        void onAddrChange(Object sender, EventArgs e)
        {
            try
            {
                updateNetworkInfo();
                NetInfo.StoreChangedNetworkSettings();
                WmiBase.Singleton.Kick();
            }
            catch (System.Management.ManagementException x) {
                if (x.ErrorCode != ManagementStatus.AccessDenied) {
                    exceptionhandler.HandleException("Network Information", x);
                }
            }
            catch (Exception ex)
            {
                exceptionhandler.HandleException("Network Information", ex);
            }
        }

        WmiWatchListener vifListen;
        WmiWatchListener vifStaticIpSettingListen;


        void onVifChanged(object nothing, EventArrivedEventArgs args)
        {
            needsRefresh = true;
        }

        void onVifStaticIpSetting(object nothing, EventArrivedEventArgs args)
        {
            needsRefresh = true;
        }

        NetworkAddressChangedEventHandler addrChangeHandler;
        public NetInfo(IExceptionHandler exceptionhandler)
        {
            updating = new Object();
            this.exceptionhandler = exceptionhandler;
            wmisession = WmiBase.Singleton.GetXenStoreSession("Adapters");
            devicevif = wmisession.GetXenStoreItem("device/vif");
            datavif = wmisession.GetXenStoreItem("data/vif");
            numvif =  wmisession.GetXenStoreItem("data/num_vif");
            vifStaticIpSetting = wmisession.GetXenStoreItem("xenserver/device/vif");
            needsRefresh = true;
            onAddrChange(null, null);
            addrChangeHandler = new NetworkAddressChangedEventHandler(onAddrChange);
            NetworkChange.NetworkAddressChanged += addrChangeHandler;
            vifListen = devicevif.Watch(onVifChanged);
            vifStaticIpSettingListen = vifStaticIpSetting.Watch(onVifStaticIpSetting);
        }

        const string NETSETTINGSSTORE = @"SOFTWARE\Citrix\XenToolsNetSettings";
        const string ENUM = @"SYSTEM\CurrentControlSet\Enum\";
        const string CONTROL = @"SYSTEM\CurrentControlSet\Control\";
        const string CLASS = CONTROL+@"Class\";
        const string NETWORKUUID = @"{4D36E972-E325-11CE-BFC1-08002BE10318}";
        const string NETWORKDEVICE = CLASS+NETWORKUUID+@"\";
        const string TCPIPSRV = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";
        const string TCPIP6SRV = @"SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters\Interfaces";
        const string NETBTSRV = @"SYSTEM\CurrentControlSet\Services\NetBT\Parameters\Interfaces";
        const string NSI = CLASS+@"NSI\";
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
                        using (RegistryKey devicekey = netdevkey.OpenSubKey(keyname)) {
                            if (((string)devicekey.GetValue("NetCfgInstanceId")).Equals(NetCfgInstanceId))
                            {
                                return keyname;
                            }
                        }
                    }
                    catch {}
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
                else {
                    devicekey.Close();
                }
             }
            throw new Exception("Unable to find Class Device Key for Mac");
        }

        static string FindClassDeviceNameForNetCfgInstanceId(string NetCfgInstanceId)
        {
            return NETWORKUUID+@"\"+FindClassDeviceKeyNameForNetCfgInstanceId(NetCfgInstanceId);
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
            
            using (RegistryKey devicekey= FindClassDeviceKeyForNetCfgInstanceId(NetCfgInstanceId)){
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
                                                        emulatedkey.SetValue(GetMacStrFromPhysical(pa), bus+"\\"+busdriver+"\\"+busdriverdevice);
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
                using (RegistryKey tcpipdestkey = Registry.LocalMachine.OpenSubKey(TCPIPSRV).OpenSubKey(Dest,true))
                {
                    CloneValues(tcpipsrckey, tcpipdestkey);
                }
            }

            using (RegistryKey tcpipsrckey = Registry.LocalMachine.OpenSubKey(TCPIP6SRV).OpenSubKey(Src))
            {
                using (RegistryKey tcpipdestkey = Registry.LocalMachine.OpenSubKey(TCPIP6SRV).OpenSubKey(Dest,true))
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
                using (RegistryKey tcpipdestkey = Registry.LocalMachine.OpenSubKey(NETBTSRV).CreateSubKey("Tcpip_"+Dest))
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
                    if (((String)store.GetValue("Status")).Equals("DontUpdate")) {
                        Trace.WriteLine("Do not update stored values");
                        return;
                    }
                }
                catch{}

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
                            Trace.WriteLine("NETINFO Save "+pa.ToString());
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
                                        string ifacetype=(string)ifacekey.GetValue("ifacetype", "NONE");
                                        Trace.WriteLine(matchmac+" ifacetype = " + ifacetype);
                                        if (! ifacetype.Equals("Emulated"))
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
            using (RegistryKey macstore = Registry.LocalMachine.CreateSubKey(NETSETTINGSSTORE+@"\Mac"))
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
                            else {
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
                    if (emustore.GetValueNames().Contains(devmac)) {
                        string EmuNetCfgInstanceId = FindNetCfgInstanceIdForDriverKey((string)emustore.GetValue(devmac));
                        string PVNetCfgInstanceId = FindNetCfgInstanceIdForDriverKey((string)pvstore.GetValue(devmac));
                        string EmuNetLuidMatchStr = FindNetLuidMatchStrForNetCfgInstanceId(EmuNetCfgInstanceId);
                        string PVNetLuidMatchStr = FindNetLuidMatchStrForNetCfgInstanceId(PVNetCfgInstanceId);

                        ClonePVStatics(STATICIPV4STORE, STATICIPV4, PVNetLuidMatchStr, EmuNetLuidMatchStr, true);
                        ClonePVStatics(STATICIPV6STORE, STATICIPV6, PVNetLuidMatchStr, EmuNetLuidMatchStr,true );
                    }   
                }
            }

        }

        public static void StoreChangedNetworkSettings()
        {
            try
            {
                ServiceController sc = null;
                ServiceControllerStatus status=ServiceControllerStatus.Stopped;
                try
                {
                    sc= new ServiceController("XenNet");
                    status = sc.Status;
                }
                catch {
                    sc = null;
                }

                if ( (sc != null) && (status == ServiceControllerStatus.Running))
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

        protected virtual void Finish()
        {
            NetworkChange.NetworkAddressChanged -= addrChangeHandler;
        }

        bool disposed = false;
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        ~NetInfo()
        {
            Dispose(false);
        }
    }
}
