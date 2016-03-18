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
using System.Management;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using NetFwTypeLib;

namespace xenwinsvc
{
    public class FeatureStaticIpSetting : Feature
    {
        XenStoreItem ipenabled;
        XenStoreItem ipv6enabled;
        XenStoreItem mac;
        XenStoreItem address;
        XenStoreItem gateway;
        XenStoreItem address6;
        XenStoreItem gateway6;
        XenStoreItem errorCode;
        XenStoreItem errorMsg;
        XenStoreItem staticIpSetting;

        public FeatureStaticIpSetting(IExceptionHandler exceptionhandler)
            : base("StaticIpSetting", "control/feature-static-ip-setting", "xenserver/device/vif", false, exceptionhandler)
        {
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
                    if (int.Parse(data.Value.ToString()) != 0)
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

            if (address.Exists() || gateway.Exists())
            {
                foreach (ManagementObject nic in WmiBase.Singleton.Win32_NetworkAdapterConfiguration)
                {
                    if (!(bool)nic["ipEnabled"])
                        continue;

                    if (nic["macAddress"].ToString().ToUpper() != macaddr.ToUpper())
                        continue;

                    try{
                        if (address.Exists())
                        {
                            string ipv4, netmask;
                            convertIpv4Mask(address.value, out ipv4, out netmask);

                            ManagementBaseObject objNewIP = nic.GetMethodParameters("EnableStatic");
                            objNewIP["IPAddress"]  = new string[] {ipv4}; 
                            objNewIP["SubnetMask"] = new string[] {netmask};

                            if (setIpv4Network(nic, "EnableStatic", objNewIP, "ipv4 address setting") != 0)
                                return;

                        }

                        if (gateway.Exists())
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
            }
        }

        private void SetStaticIpv6Setting()
        {
            string macaddr = mac.value;

            resetError();

            if (address6.Exists() || gateway6.Exists())
            {
                foreach (ManagementObject nic in WmiBase.Singleton.Win32_NetworkAdapterConfiguration)
                {
                    if (!(bool)nic["ipEnabled"])
                        continue;

                    if (nic["macAddress"].ToString().ToUpper() != macaddr.ToUpper())
                        continue;

                    try{
                        if (address6.Exists())
                        {
                            string argument = "interface ipv6 set address {0} {1}";
                            argument = string.Format(argument, nic["interfaceIndex"], address6.value);

                            if (netshInvoke(argument) != 0)
                                return;
                        }

                        if (gateway6.Exists())
                        {
                            string argument = "interface ipv6 add route ::/0 {0} {1}";
                            argument = string.Format(argument, nic["interfaceIndex"], gateway6.value);

                            if (netshInvoke(argument) != 0)
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

                try{
                    setIpv4Network(nic, "EnableDHCP", null, "set back to dhcp");
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

                try{
                    string argument = "interface ipv6 reset";
                    netshInvoke(argument);
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
                foreach (string vif in staticIpSetting.children)
                {

                    try
                    {
                        mac = wmisession.GetXenStoreItem(vif + "/static-ip-setting/mac");
                        ipenabled   = wmisession.GetXenStoreItem(vif + "/static-ip-setting/enabled");
                        ipv6enabled = wmisession.GetXenStoreItem(vif + "/static-ip-setting/enabled6");
                        errorCode = wmisession.GetXenStoreItem(vif + "/static-ip-setting/error-code");
                        errorMsg  = wmisession.GetXenStoreItem(vif + "/static-ip-setting/error-msg");

                        if (ipenabled.Exists())
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

                            ipenabled.Remove();
                        }

                        if (ipv6enabled.Exists())
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
                            
                            ipv6enabled.Remove();
                        }
                    }
                    catch { }; // Ignore failure, if node does not exist


                }
            }
        }
    }
}
