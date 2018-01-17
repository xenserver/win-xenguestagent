﻿/* Copyright (c) Citrix Systems Inc.
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
using System.Management;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using NetFwTypeLib;

namespace xenwinsvc
{
    public class IpSettingItem
    {
        public IpSettingItem(string mac, string ipversion, string DHCPEnable, string ip, string mask, string gateway)
        {
            _mac = mac;
            _ipversion = ipversion;
            _DHCPEnable = DHCPEnable;
            _ip = ip;
            _mask = mask;
            _gateway = gateway;
        }
        private string _mac;
        public string MAC
        {
            set { _mac = value; }
            get { return _mac; }
        }
        private string _ipversion;
        public string IPVERSION
        {
            set { _ipversion = value; }
            get { return _ipversion; }
        }
        private string _DHCPEnable;
        public string DHCPENABLE
        {
            set { _DHCPEnable = value; }
            get { return _DHCPEnable; }
        }
        private string _ip;
        public string IP
        {
            set { _ip = value; }
            get { return _ip; }
        }
        private string _mask;
        public string MASK
        {
            set { _mask = value; }
            get { return _mask; }
        }
        private string _gateway;
        public string GATEWAY
        {
            set { _gateway = value; }
            get { return _gateway; }
        }
    }

    public class IpSettings
    {
        static List <IpSettingItem> iplist = new List<IpSettingItem>();
        private static readonly string IP_INFO_REGISTRY_KEY_PATH = "HKEY_LOCAL_MACHINE\\Software\\Citrix\\Xentools";
        private static readonly string IP_INFO_REGISTRY_VALUE_NAME = "ipSettings";
        private static readonly string IP_INFO_COMPONENT_SEPARATOR = ","; // separate component of a ip configurations, like mac, ipv4/ipv6, etc
        private static readonly int IP_INFO_COMPONENT_NUMBER = 5; // the number of the ip info components, like mac, ipv4/6, etc
  
        // Add new ip information and save into registry
        public static void addIpSeting(string mac, string DHCPEnable, string ipversion, string ip, string mask, string gateway)
        {
            bool existed = false;
            foreach (var ipsetting in iplist)
            {
                if (ipsetting.MAC == mac && ipsetting.IPVERSION == ipversion)
                {
                    existed = true;
                    break;
                }
            }
            if( existed == false )
            {
                iplist.Add(new IpSettingItem(mac, ipversion, DHCPEnable, ip, mask, gateway));
                Save();
            }
        }

        // Get ip address from list, match if mac and ipversion match
        public static bool getIPseting(string mac, string ipversion, ref IpSettingItem settings)
        {
            foreach (var ipsetting in iplist)
            {
                if (ipsetting.MAC == mac && ipsetting.IPVERSION == ipversion)
                {
                    settings.DHCPENABLE = ipsetting.DHCPENABLE;
                    settings.IP = ipsetting.IP;
                    settings.GATEWAY = ipsetting.GATEWAY;
                    settings.MASK = ipsetting.MASK;
                    return true;
                }
            }
            return false;
        }

        // Remove an ip information and save to registry
        public static bool removeIPseting(string mac, string ipversion)
        {
            foreach (var ipsetting in iplist)
            {
                if (ipsetting.MAC == mac && ipsetting.IPVERSION == ipversion)
                {
                    iplist.Remove(ipsetting);
                    Save();
                    return true;
                }
            }
            return false;
        }

       // Load saved ip infomation from register to datastructure
        public static void load()
        {
            
            string[] strIpInfoArray = (string[]) Registry.GetValue(IP_INFO_REGISTRY_KEY_PATH,IP_INFO_REGISTRY_VALUE_NAME,null);

            if(null ==  strIpInfoArray ) return; // The registry key  or the registry name does not exist. 

            foreach (var item  in strIpInfoArray)
            {
                string[] infors = item.Split(new string[] {IP_INFO_COMPONENT_SEPARATOR}, StringSplitOptions.None);
                if (infors.Length >= IP_INFO_COMPONENT_NUMBER)
                {
                    iplist.Add(new IpSettingItem(infors[0], infors[1], infors[2], infors[3], infors[4], infors[5]));
                }
             
            }
        }

        // Save the ip information to registry
        private static void Save()
        { 
            // The ip Informations would be sored int the reigstry in following format
            // Mac, ipv4/ipv6, "DHCPEnable", "ip", "mask", "gatway" 
            // Mac, ipv4/ipv6, "DHCPEnable", "ip", "mask", "gatway"
            var strIpList = new List<string>();
            foreach (var setting in iplist)
            {
                string ipInfoRegistryValue = "";
                ipInfoRegistryValue = ipInfoRegistryValue + setting.MAC + IP_INFO_COMPONENT_SEPARATOR  + setting.IPVERSION + IP_INFO_COMPONENT_SEPARATOR + setting.DHCPENABLE + IP_INFO_COMPONENT_SEPARATOR + setting.IP + IP_INFO_COMPONENT_SEPARATOR + setting.MASK + IP_INFO_COMPONENT_SEPARATOR + setting.GATEWAY;
                strIpList.Add(ipInfoRegistryValue);
            }
      
            Registry.SetValue(IP_INFO_REGISTRY_KEY_PATH,IP_INFO_REGISTRY_VALUE_NAME,strIpList.ToArray());
        
        }

    }
    public class FeatureStaticIpSetting : Feature
    {
        AXenStoreItem ipenabled;
        AXenStoreItem ipv6enabled;
        AXenStoreItem mac;
        AXenStoreItem address;
        AXenStoreItem gateway;
        AXenStoreItem address6;
        AXenStoreItem gateway6;
        AXenStoreItem errorCode;
        AXenStoreItem errorMsg;
        AXenStoreItem staticIpSetting;

        public FeatureStaticIpSetting(IExceptionHandler exceptionhandler)
            : base("StaticIpSetting", "control/feature-static-ip-setting", "xenserver/device/vif", false, exceptionhandler)
        {
            IpSettings.load();
            staticIpSetting = wmisession.GetXenStoreItem("xenserver/device/vif");
        }

        private void resetError()
        {
            errorCode.value = "0";
            errorMsg.value = "";
        }

        private string ipv4BitsToMask(int bits)
        {
            uint tmpmask; string netmask;
            tmpmask = ~(0xffffffff >> bits);
            netmask=((tmpmask & 0xff000000) >> 24).ToString() + "." +
                ((tmpmask & 0xff0000) >> 16).ToString() + "." +
                ((tmpmask & 0xff00)>>8).ToString() + "." +
                ((tmpmask&0xff)).ToString();
            return netmask;
        }

        private void convertIpv4Mask(string address, out string ip, out string netmask)
        {
            int bits;

            string[] elements = address.Split(new Char[] {'/'});
            ip = elements[0];

            bits = Convert.ToInt32(elements[1]);
            netmask = ipv4BitsToMask(bits);
        }

        private int setIpv4Network(ManagementObject nic, string method, ManagementBaseObject setting, string msgprefix)
        {
            ManagementBaseObject setIP;

            setIP = nic.InvokeMethod(method, setting, null);

            foreach (PropertyData data in setIP.Properties)
            {
                if (data.Name.ToLower() == "returnvalue")
                {
                    if (uint.Parse(data.Value.ToString()) != 0)
                    {
                        errorCode.value = data.Value.ToString();
                        errorMsg.value = msgprefix + " failure";
                        return 1;
                    }
                }
            }
            
            return 0;
        }

        private int netshInvoke(string argument)
        {
            Process myProcess;
            int retVal; string retMsg;

            ProcessStartInfo procStartInfo = new ProcessStartInfo
            {
                WorkingDirectory = Path.GetPathRoot(Environment.SystemDirectory),
                FileName = Path.Combine(Environment.SystemDirectory, "netsh.exe"),
                Arguments = argument,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            myProcess = Process.Start(procStartInfo);
            myProcess.WaitForExit();
            retVal = myProcess.ExitCode;
            retMsg = "";
            if (retVal != 0)
                retMsg = myProcess.StandardOutput.ReadToEnd();
            myProcess.Close();

            if (retVal != 0)
            {
                errorCode.value = Convert.ToString(retVal);
                errorMsg.value = "[ipv6] " + retMsg;
            }
            return retVal;
        }

        private void SetStaticIpv4Setting()
        {
            string macaddr = mac.value;

            resetError();

            if ((address.Exists() && address.value.Length != 0) || (gateway.Exists() && gateway.value.Length != 0))
            {
                bool FoundDevice = false;
                foreach (ManagementObject nic in WmiBase.Singleton.Win32_NetworkAdapterConfiguration)
                {
                    if (!(bool)nic["ipEnabled"])
                        continue;

                    if (nic["macAddress"].ToString().ToUpper() != macaddr.ToUpper())
                        continue;

                    FoundDevice = true;

                    IpSettings.addIpSeting(nic["macAddress"].ToString(), nic["DHCPEnabled"].ToString(), "IPV4", "", "", "");

                    try{
                        if (address.Exists() && address.value.Length != 0)
                        {
                            string ipv4, netmask;
                            convertIpv4Mask(address.value, out ipv4, out netmask);

                            ManagementBaseObject objNewIP = nic.GetMethodParameters("EnableStatic");
                            objNewIP["IPAddress"]  = new string[] {ipv4}; 
                            objNewIP["SubnetMask"] = new string[] {netmask};

                            if (setIpv4Network(nic, "EnableStatic", objNewIP, "ipv4 address setting") != 0)
                                return;

                        }

                        if (gateway.Exists() && gateway.value.Length != 0)
                        {
                            ManagementBaseObject objNewGate = nic.GetMethodParameters("SetGateways");
                            objNewGate["DefaultIPGateway"]  = new string[] {gateway.value};

                            if (setIpv4Network(nic, "SetGateways", objNewGate, "ipv4 gateway setting") != 0)
                                return;
                        }
                    }
                    catch (Exception e)
                    {
                        errorCode.value = "1";
                        errorMsg.value = e.ToString();

                        wmisession.Log("Exception " + e.ToString());
                        return;
                    }
                }

                if (!FoundDevice)
                {
                    errorCode.value = "101";
                    errorMsg.value = "Device not ready to use or ipEnabled not been set";
                    wmisession.Log("Device not ready to use or ipEnabled not been set");
                    return;
                }
            }
        }

        private void SetStaticIpv6Setting()
        {
            string macaddr = mac.value;

            resetError();

            if ((address6.Exists() && address6.value.Length != 0) || (gateway6.Exists() && gateway6.value.Length != 0))
            {
                bool FoundDevice = false;
                foreach (ManagementObject nic in WmiBase.Singleton.Win32_NetworkAdapterConfiguration)
                {
                    if (!(bool)nic["ipEnabled"])
                        continue;

                    if (nic["macAddress"].ToString().ToUpper() != macaddr.ToUpper())
                        continue;

                    FoundDevice = true;

                    IpSettings.addIpSeting(nic["macAddress"].ToString(), nic["DHCPEnabled"].ToString(), "IPV6", "", "", "");
                    
                    try{
                        if (address6.Exists() && address6.value.Length != 0)
                        {
                            string argument = "interface ipv6 set address {0} {1}";
                            argument = string.Format(argument, nic["interfaceIndex"], address6.value);

                            if (netshInvoke(argument) != 0)
                                return;
                        }

                        if (gateway6.Exists() && gateway6.value.Length != 0)
                        {
                            string argument = "interface ipv6 add route ::/0 {0} {1}";
                            argument = string.Format(argument, nic["interfaceIndex"], gateway6.value);

                            if (netshInvoke(argument) != 0)
                            {
                                resetError();
                                argument = "interface ipv6 set route ::/0 {0} {1}";
                                argument = string.Format(argument, nic["interfaceIndex"], gateway6.value);

                                if (netshInvoke(argument) != 0)
                                    return;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        errorCode.value = "1";
                        errorMsg.value = e.ToString();

                        wmisession.Log("Exception " + e.ToString());
                        return;
                    }
                }
                if (!FoundDevice)
                {
                    errorCode.value = "101";
                    errorMsg.value = "Device not ready to use or ipEnabled not been set";
                    wmisession.Log("Device not ready to use or ipEnabled not been set");
                    return;
                }
            }
        }

        private void UnsetStaticIpv4Setting()
        {
            string macaddr = mac.value;

            resetError();

            foreach (ManagementObject nic in WmiBase.Singleton.Win32_NetworkAdapterConfiguration)
            {
                if (!(bool)nic["ipEnabled"])
                    continue;

                if (nic["macAddress"].ToString().ToUpper() != macaddr.ToUpper())
                    continue;

                IpSettingItem settings = new IpSettingItem(nic["macAddress"].ToString(), "IPV4", "", "", "", "");
                if (IpSettings.getIPseting(nic["macAddress"].ToString(), "IPV4", ref settings) == false)
                    return;

                try{
                    setIpv4Network(nic, "EnableDHCP", null, "set back to dhcp");
                    IpSettings.removeIPseting(nic["macAddress"].ToString(), "IPV4");
                }
                catch (Exception e)
                {
                    errorCode.value = "1";
                    errorMsg.value = e.ToString();

                    wmisession.Log("Exception " + e.ToString());
                    return;
                }
            }
        }

        private void UnsetStaticIpv6Setting()
        {
            string macaddr = mac.value;

            resetError();

            foreach (ManagementObject nic in WmiBase.Singleton.Win32_NetworkAdapterConfiguration)
            {
                if (!(bool)nic["ipEnabled"])
                    continue;

                if (nic["macAddress"].ToString().ToUpper() != macaddr.ToUpper())
                    continue;

                IpSettingItem settings = new IpSettingItem(nic["macAddress"].ToString(), "IPV6", "", "", "", "");
                if (IpSettings.getIPseting(nic["macAddress"].ToString(), "IPV6", ref settings) == false)
                    return;

                try{
                    string argument = "interface ipv6 reset";
                    netshInvoke(argument);
                    IpSettings.removeIPseting(nic["macAddress"].ToString(), "IPV6");
                }
                catch (Exception e)
                {
                    errorCode.value = "1";
                    errorMsg.value = e.ToString();

                    wmisession.Log("Exception " + e.ToString());
                    return;
                }
            }
        }


        protected override void onFeature()
        {
            if (controlKey.Exists())
            {
                try
                {
                    foreach (string vif in staticIpSetting.children)
                    {
                        mac = wmisession.GetXenStoreItem(vif + "/static-ip-setting/mac");
                        ipenabled   = wmisession.GetXenStoreItem(vif + "/static-ip-setting/enabled");
                        ipv6enabled = wmisession.GetXenStoreItem(vif + "/static-ip-setting/enabled6");
                        errorCode = wmisession.GetXenStoreItem(vif + "/static-ip-setting/error-code");
                        errorMsg  = wmisession.GetXenStoreItem(vif + "/static-ip-setting/error-msg");

                        if (ipenabled.Exists() && ipenabled.value.Length != 0)
                        {
                            if (int.Parse(ipenabled.value) == 1) // assign static ip setting
                            {
                                address  = wmisession.GetXenStoreItem(vif + "/static-ip-setting/address");
                                gateway  = wmisession.GetXenStoreItem(vif + "/static-ip-setting/gateway");

                                SetStaticIpv4Setting();

                                wmisession.Log("Static ip setting is assigned.");
                            }
                            else // remove static ip setting
                            {
                                UnsetStaticIpv4Setting();

                                wmisession.Log("Static ip setting is unassigned.");
                            }
                        }
                        if (ipenabled.Exists()) ipenabled.Remove();

                        if (ipv6enabled.Exists() && ipv6enabled.value.Length != 0)
                        {
                            if (int.Parse(ipv6enabled.value) == 1) // assign static ipv6 setting
                            {
                                address6 = wmisession.GetXenStoreItem(vif + "/static-ip-setting/address6");
                                gateway6 = wmisession.GetXenStoreItem(vif + "/static-ip-setting/gateway6");

                                SetStaticIpv6Setting();

                                wmisession.Log("Static ipv6 setting is assigned.");
                            }
                            else // remove static ipv6 setting
                            {
                                UnsetStaticIpv6Setting();

                                wmisession.Log("Static ipv6 setting is unassigned.");
                            }
                        }
                        if (ipv6enabled.Exists()) ipv6enabled.Remove();
                    }
                }
            catch { }; // Ignore failure, if node does not exist
            }
        }
    }
}
