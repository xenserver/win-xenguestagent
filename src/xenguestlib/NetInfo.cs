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
            return true;
        }

        void onAddrChange(Object sender, EventArgs e)
        {
            try
            {
                updateNetworkInfo();
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


        void onVifChanged(object nothing, EventArrivedEventArgs args)
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
            needsRefresh = true;
            onAddrChange(null, null);
            addrChangeHandler = new NetworkAddressChangedEventHandler(onAddrChange);
            NetworkChange.NetworkAddressChanged += addrChangeHandler;
            vifListen = devicevif.Watch(onVifChanged);
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
