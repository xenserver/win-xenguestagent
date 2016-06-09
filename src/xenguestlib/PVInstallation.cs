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
using System.IO.Pipes;
namespace xenwinsvc
{
    public class PVInstallation : Feature, IRefresh
    {
        XenStoreItem osname;
        XenStoreItem hostname;
        XenStoreItem hostnamedns;
        XenStoreItem domain;


        XenStoreItem osclass;
        XenStoreItem osmajor;
        XenStoreItem osminor;
        XenStoreItem osbuild;
        XenStoreItem osplatform;
        XenStoreItem osspmajor;
        XenStoreItem osspminor;
        XenStoreItem ossuite;
        XenStoreItem ostype;
        XenStoreItem datadistro;
        XenStoreItem datamajor;
        XenStoreItem dataminor;
        const string attrwinnt = "windows NT";
        const string distwindows = "windows";

        XenStoreItem osboottype;
        XenStoreItem ossystem32;
        XenStoreItem oshal;
        XenStoreItem osbootoptions;

        XenStoreItem oslicense;
        XenStoreItem osvirtualxp;



        XenStoreItem pvmajor;
        XenStoreItem pvminor;
        XenStoreItem pvmicro;
        XenStoreItem pvbuild;
        XenStoreItem pvinstalled;
        XenStoreItem guestdotnetframework;

        XenStoreItem xdvdapresent;
        XenStoreItem xdvdaproductinstalled;

        object pvinstalllock = new object();
        bool initialised = false;

        override protected void onFeature()
        {
            lock (pvinstalllock)
            {
                while (!initialised)
                {
                    System.Threading.Monitor.Wait(pvinstalllock);
                }
                if (!registered)
                {
                    RefreshXenstore();
                }
            }
        }
        bool xenwinsvc.IRefresh.NeedsRefresh()
        {
            while (!initialised)
            {
                System.Threading.Monitor.Wait(pvinstalllock);
            }
            if (needsinstalling)
                return true;
            if (pvinstalledStatus != FeatureLicensed.IsLicensed()){
            // For license status not match the PVAddon registe status, we need to refresh the status of PVAddon
                return true;
            }
            return false;
         
        }
        bool xenwinsvc.IRefresh.Refresh(bool force)
        {
            lock (pvinstalllock)
            {
                while (!initialised)
                {
                    System.Threading.Monitor.Wait(pvinstalllock);
                }
                if (force)
                {
                    Feature.Advertise(wmisession);
                }
                if ((needsinstalling && !installing())
                    || ((pvinstalledStatus != FeatureLicensed.IsLicensed())
                         && (pvinstalledStatus == true)))
                {
                    RefreshXenstore();
                    return true;
                }
                return false;
            }
        }

        protected override void Finish() 
        {
            registered = false;
            pvmajor.Remove();
            pvminor.Remove();
            pvmicro.Remove();
            pvbuild.Remove();
            WmiBase.Singleton.Kick();
            base.Finish();
        }

        void RefreshXenstore()
        {
            Feature.Advertise(wmisession);
            RegisterPVAddons();
        }
        public bool installing()
        {
            try
            {
                string installstate;
                if (Win32Impl.is64BitOS() && (!Win32Impl.isWOW64()))
                {
                    installstate = (string)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\Citrix\\XenToolsInstaller", "InstallStatus", "Installed");
                }
                else
                {
                    installstate = (string)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Citrix\\XenToolsInstaller", "InstallStatus", "Installed");
                }
                if (installstate == null)
                    installstate = "Installed";

                if (installstate.Equals("Installed"))
                {
                    needsinstalling = false;
                    return false;
                }
                needsinstalling = true;
                return true;
            }
            catch
            {
                needsinstalling = true;
                return true;
            }
        }
        volatile bool pvinstalledStatus = false;
        public bool registered
        {
            get
            {
                if (pvinstalled.Exists() && pvinstalled.value.Equals("1") && FeatureLicensed.IsLicensed())
                    return true;
                return false;
            }
            set
            {
                if (value)
                {

                    if (!installing() && FeatureLicensed.IsLicensed())
                    {
                        pvinstalled.value = "1";
                        pvinstalledStatus = true;
                    }
                }
                else
                {
                    if (pvinstalled.Exists())
                    {
                        pvinstalled.Remove();
                    }
                    pvinstalledStatus = false;
                }
            }
        }
        bool needsinstalling;
        public PVInstallation(IExceptionHandler exceptionhandler)
            : base("PV Installation", "", "attr/PVAddons/Installed", false, exceptionhandler)
        {
            
            osclass = wmisession.GetXenStoreItem("attr/os/class");
            osmajor = wmisession.GetXenStoreItem("attr/os/major");
            osminor = wmisession.GetXenStoreItem("attr/os/minor");
            osbuild = wmisession.GetXenStoreItem("attr/os/build");
            osplatform = wmisession.GetXenStoreItem("attr/os/platform");
            osspmajor = wmisession.GetXenStoreItem("attr/os/spmajor");
            osspminor = wmisession.GetXenStoreItem("attr/os/spminor");
            ossuite = wmisession.GetXenStoreItem("attr/os/suite");
            ostype = wmisession.GetXenStoreItem("attr/os/type");
            datadistro = wmisession.GetXenStoreItem("data/os_distro");
            datamajor = wmisession.GetXenStoreItem("data/os_majorver");
            dataminor = wmisession.GetXenStoreItem("data/os_minorver");
            guestdotnetframework = wmisession.GetXenStoreItem("data/guest_dotnet_framework");

            osboottype = wmisession.GetXenStoreItem("attr/os/boottype");
            ossystem32 = wmisession.GetXenStoreItem("attr/os/system32_dir");
            oshal = wmisession.GetXenStoreItem("attr/os/hal");
            osbootoptions = wmisession.GetXenStoreItem("attr/os_boot/options");


            osname = wmisession.GetXenStoreItem("data/os_name");
            hostname = wmisession.GetXenStoreItem("data/host_name");
            hostnamedns = wmisession.GetXenStoreItem("data/host_name_dns");
            domain = wmisession.GetXenStoreItem("data/domain");

            oslicense = wmisession.GetXenStoreItem("attr/os/license");
            osvirtualxp = wmisession.GetXenStoreItem("attr/os/virtualxp_enabled");

            pvmajor = wmisession.GetXenStoreItem("attr/PVAddons/MajorVersion");
            pvminor = wmisession.GetXenStoreItem("attr/PVAddons/MinorVersion");
            pvmicro = wmisession.GetXenStoreItem("attr/PVAddons/MicroVersion");
            pvbuild = wmisession.GetXenStoreItem("attr/PVAddons/BuildVersion");
            pvinstalled = wmisession.GetXenStoreItem("attr/PVAddons/Installed");

            xdvdapresent = wmisession.GetXenStoreItem("data/xd/present");
            xdvdaproductinstalled = wmisession.GetXenStoreItem("data/xd/product_installed");

            lock (pvinstalllock)
            {
                registered = false;
                needsinstalling = true;
                initialised = true;
                System.Threading.Monitor.PulseAll(pvinstalllock);
                RefreshXenstore();
            }
        }

        public void RegisterPVAddons()
        {
            registered = FeatureLicensed.IsLicensed();
            try
            {
                if (!installing())
                {
                    pvmajor.value = ((int)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Citrix\\XenTools", "MajorVersion", 0)).ToString();
                    pvminor.value = ((int)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Citrix\\XenTools", "MinorVersion", 0)).ToString();
                    pvmicro.value = ((int)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Citrix\\XenTools", "MicroVersion", 0)).ToString();
                    pvbuild.value = ((int)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Citrix\\XenTools", "BuildVersion", 0)).ToString();
                    WmiBase.Singleton.Kick();
                }
            }
            catch (Exception e)
            {
                wmisession.Log("Setting system version values failed: \n" + e.ToString());
                throw;
            }
            addSystemInfoToStore();
            addXDInfoToStore();
            WmiBase.Singleton.Kick();
        }

        void addXDInfoToStore()
        {
            try
            {
                string vdapath;
                if (Win32Impl.is64BitOS() && (!Win32Impl.isWOW64()))
                {
                    vdapath = "Software\\Wow6432Node\\Citrix\\VirtualDesktopAgent";
                }
                else {
                    vdapath = "Software\\Citrix\\VirtualDesktopAgent";
                }
                try
                {
                    if (Array.Exists(Registry.LocalMachine.OpenSubKey(vdapath).GetValueNames(),
                        delegate(string s) { return s.Equals("ListOfDDCs"); }))
                    {
                        xdvdapresent.value = "1";
                    }
                    else
                    {
                        // ListOfDDCs not found
                        xdvdapresent.value = "0";
                    }

                    try
                    {
                        if (Registry.LocalMachine.OpenSubKey(vdapath).GetValueKind("ProductInstalled") == RegistryValueKind.DWord)
                        {
                            try
                            {
                                xdvdaproductinstalled.value = ((int)Registry.LocalMachine.OpenSubKey(vdapath).GetValue("ProductInstalled")).ToString();
                            }
                            catch (Exception e)
                            {
                                wmisession.Log("addXDInfoToStore Can't read ProductInstalled : " + e.ToString());
                            }
                        }
                        else
                        {
                            wmisession.Log("addXDInfoToStore ProductInstalled is not a DWORD");
                        }
                    }
                    catch
                    {
                        //ProductInstalled doesn't exist
                    }

                }
                catch
                {
                    // Unable to read vdapath
                    xdvdapresent.value = "0";
                }
                
            }
            catch(Exception e)
            {
                wmisession.Log("addXDInfoToStore Failed : "+e.ToString());
            }
        }

        void addSystemInfoToStore()
        {
            osclass.value = attrwinnt;
            Win32Impl.WinVersion wv = new Win32Impl.WinVersion();
            OperatingSystem os = Environment.OSVersion;
            osmajor.value = os.Version.Major.ToString();
            osminor.value = os.Version.Minor.ToString();
            osbuild.value = os.Version.Build.ToString();
            osplatform.value = wv.GetPlatformId().ToString();
            osspmajor.value = wv.GetServicePackMajor().ToString();
            osspminor.value = wv.GetServicePackMinor().ToString();
            ossuite.value = wv.GetSuite().ToString();
            ostype.value = wv.GetProductType().ToString();
            datadistro.value = distwindows;
            datamajor.value = os.Version.Major.ToString();
            dataminor.value = os.Version.Minor.ToString();

            osboottype.value = System.Windows.Forms.SystemInformation.BootMode.ToString();
            ossystem32.value = Environment.GetFolderPath(Environment.SpecialFolder.System);

            FileVersionInfo halinfo = FileVersionInfo.GetVersionInfo(Environment.GetFolderPath(Environment.SpecialFolder.System) + "\\hal.dll");
            oshal.value = halinfo.InternalName;
            osbootoptions.value = (string)Registry.GetValue("HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Control", "SystemStartOptions", "none");
            guestdotnetframework.value = System.Environment.Version.ToString();
            
            DumpOsData();
            addHotFixInfoToStore();
            addLicenseInfoToStore();
        }


        const string licensegenuine = "genuine";
        const string licenseinvalid = "invalid";
        const string licensetampered = "tampered";

        void addLicenseInfoToStore()
        {
            try
            {
                if (oslicense.Exists())
                {
                    oslicense.Remove();
                }
                switch (Win32Impl.IsGenuineWindows())
                {
                    case Win32Impl.SL_GENUINE_STATE.SL_GEN_STATE_INVALID_LICENSE:
                        oslicense.value = licenseinvalid;
                        break;
                    case Win32Impl.SL_GENUINE_STATE.SL_GEN_STATE_IS_GENUINE:
                        oslicense.value = licensegenuine;
                        break;
                    case Win32Impl.SL_GENUINE_STATE.SL_GEN_STATE_TAMPERED:
                        oslicense.value = licensetampered;
                        break;
                    default:
                        break;
                }
            }
            catch { //Do nothing if IsGenuineWindows doesn't exist
            };
            try
            {
                if (osvirtualxp.Exists())
                {
                    osvirtualxp.Remove();
                }
                if (Win32Impl.GetWindowsInformation("VirtualXP-licensing-Enabled") != 0)
                {
                    osvirtualxp.value = "1";
                }
                else
                {
                    osvirtualxp.value = "0";
                }
            }
            catch (Exception e)
            {
                WmiBase.Singleton.DebugMsg("GetWindowsInformation failed: \n" + e.ToString());
            }
        }

        bool enablehotfixinfo = true;
        void addHotFixInfoToStore()
        {
            uint index = 0;
            if (enablehotfixinfo) {
                foreach (ManagementObject mo in WmiBase.Singleton.Win32_QuickFixEngineering)
                {
                    // Ignore Hotfixes where the id has been replaced by "File 1"
                    // Because these hotfixes have been replaced
                    string id = (string)mo["HotFixID"];
                    if (!id.Equals("File 1"))
                    {
                        XenStoreItem hotfix = wmisession.GetXenStoreItem("attr/os/hotfixes/" + index.ToString());
                        hotfix.value = id;
                        if (hotfix.GetStatus() == ManagementStatus.AccessDenied) {
                            enablehotfixinfo = false;
                        }
                        index++;
                    }
                }
            }
        }

        void DumpOsData()
        {
            osname.value = (string)WmiBase.Singleton.Win32_OperatingSystem["Name"];
            hostname.value = (string)WmiBase.Singleton.Win32_ComputerSystem["Name"];
            hostnamedns.value = Win32Impl.GetComputerDnsHostname();
            domain.value = (string)WmiBase.Singleton.Win32_ComputerSystem["Domain"];
        }

    }

}
