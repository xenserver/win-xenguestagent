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
using System.Diagnostics;
using System.Threading;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace xenwinsvc
{
    
    public class FeatureAutoUpdate : Feature, IRefresh
    {
        const string MSI_URL = "http://10.80.239.142/";
        static volatile Thread autoUpdateThread = null;
        static object threadlock = new object();
        static bool stopping = false;
        
        public static void JoinAutoUpdate()
        {
            lock (threadlock)
            {
                stopping = true;
                if (autoUpdateThread != null)
                {
                    autoUpdateThread.Join();
                    autoUpdateThread = null;
                }
            }
        }

        bool VerifyCertificate(string filename)
        {
            X509Certificate2 theCertificate;

            try
            {
                X509Certificate theSigner = X509Certificate.CreateFromSignedFile(filename);
                if(!theSigner.Subject.Contains("O=\"Citrix Systems, Inc.\""))
                    return false;
                theCertificate = new X509Certificate2(theSigner);
                var theCertificateChain = new X509Chain();
                theCertificateChain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
                theCertificateChain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                theCertificateChain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 1, 0);
                theCertificateChain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
                return theCertificateChain.Build(theCertificate);
            }
            catch (Exception e)
            {
                wmisession.Log("VerifyCertificate failed with: \n" + e.ToString());
                return false;
            }
        }

        XenStoreItem downloadURLKey;

        string downloadMSI()
        {
            Regex regex = new Regex("<a href=\".*\">(?<name>.*)</a>");
            string downloadURL = MSI_URL;
            bool needUpdate = false;
            if (downloadURLKey.Exists())
                downloadURL = downloadURLKey.value;
            WebClient client = new WebClient();
            string content = client.DownloadString(downloadURL);
            
            MatchCollection matches = regex.Matches(content);
            string version_prefix = "version_";
            if (matches.Count > 0)
            {
                foreach (Match match in matches)
                {
                    if (match.Success)
                    {
                        string filename = match.Groups["name"].ToString();
                        if (filename.StartsWith(version_prefix))
                        {
                            string version = filename.Substring(version_prefix.Length);
                            List<string> versionArray = new List<string>(version.Split('.'));
                            if (versionArray.Count < 4)
                                return null;
                            int newMajor = Convert.ToInt32(versionArray[0]);
                            int newMinor = Convert.ToInt32(versionArray[1]);
                            int newMicro = Convert.ToInt32(versionArray[2]);
                            int newBuild = Convert.ToInt32(versionArray[3]);
                            int major = ((int)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Citrix\\XenTools", "MajorVersion", 0));
                            int minor = ((int)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Citrix\\XenTools", "MinorVersion", 0));
                            int micro = ((int)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Citrix\\XenTools", "MicroVersion", 0));
                            int build = ((int)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Citrix\\XenTools", "BuildVersion", 0));

                            Version newVersion = new Version(newMajor, newMinor, newMicro, newBuild);
                            Version oldVersion = new Version(major, minor, micro, build);
                            wmisession.Log("onFeature: doAgentAutoUpdate \n" + filename);
                            wmisession.Log("newVersion " + newVersion.ToString());
                            wmisession.Log("Version " + oldVersion.ToString());
                            if (newVersion.CompareTo(oldVersion) > 0)
                                needUpdate = true;
                        }
                    }
                }
            }

            if (needUpdate)
            {
                string msiName;
                if (Win32Impl.is64BitOS() && (!Win32Impl.isWOW64()))
                    msiName = "citrixguestagentx64.msi";
                else
                    msiName = "citrixguestagentx86.msi";

                string msiURL = downloadURL + msiName;


                string msi = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\" + msiName;
                if (File.Exists(msi))
                    File.Delete(msi);
                client.DownloadFile(new Uri(msiURL), msi);
                wmisession.Log("downloadMSI:" + downloadURL + " successful");
                if (VerifyCertificate(msi))
                    return msi;
                else
                    return null;
            }
            else
                return null;
        }

        void autoUpdateHandler()
        {
            string msiName = downloadMSI();
            string installdir;
            if (string.IsNullOrEmpty(msiName))
                return;
            
            if (Win32Impl.is64BitOS())
                installdir = (string)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\Citrix\\XenToolsInstaller", "Install_Dir", Application.StartupPath);
            else
                installdir = (string)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Citrix\\XenToolsInstaller", "Install_Dir", Application.StartupPath);
            
            string logfile = "\"" + installdir + "\\" + "agent3msi" + "\"";
            installdir = "\"" + installdir + "\"";
            
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "msiexec.exe";
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardOutput = true;

            startInfo.Arguments = " /i " + msiName + " TARGETDIR=" + installdir + " /log " + logfile + " /qn";            
            isUpdating = true;
            foreach (var process in Process.GetProcessesByName("XenDpriv.exe"))
            {
                process.Kill();
            }
            wmisession.Log("install update with:" + startInfo.Arguments);
            Process newprocess = Process.Start(startInfo);
        }

        void doAutoUpdate()
        {
            lock (threadlock)
            {
                if (stopping)
                {
                    WmiBase.Singleton.DebugMsg("Not starting new auto update thread, feature is stopping");
                    return;
                }
                if (autoUpdateThread != null)
                {
                    autoUpdateThread.Join();
                }
                try
                {
                    autoUpdateThread = new Thread(autoUpdateHandler, 256 * 1024);
                    autoUpdateThread.SetApartmentState(ApartmentState.STA);
                    autoUpdateThread.Start();
                }
                catch
                {
                    WmiBase.Singleton.SetError("CreateThread Agent Auto Update");
                }
            }
        }

        public FeatureAutoUpdate(IExceptionHandler exceptionhandler)
            : base("autoUpdate", "", "control/auto-update-agent", true, exceptionhandler)
        {
            downloadURLKey = wmisession.GetXenStoreItem("control/downloadURL");
        }

        volatile bool featureEnable = false;
        override protected void onFeature()
        {
            int DisableAutoUpdate = ((int)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Citrix\\XenTools", "DisableAutoUpdate", 0));
            if (DisableAutoUpdate == 1)
                return;

            int enableval = 0;
            if (controlKey.Exists())
            {
                try
                {
                    enableval = int.Parse(controlKey.value);
                }
                catch
                {
                    enableval = 0;
                }
            }
            refreshCount = 0;
            featureEnable = enableval != 0 ? true : false;
            if (featureEnable)
            {
                wmisession.Log("onFeature: doAgentAutoUpdate \n");
                doAutoUpdate();
            }
        }
        //do update check after start
        int refreshCount = 19200;
        public bool NeedsRefresh()
        {//update date for each 24 hours
            refreshCount++;
            if (refreshCount > 19200)
            {

                wmisession.Log("Refresh: need \n");
                refreshCount = 0;
                return true;
            }
            return false;
        }

        bool xenwinsvc.IRefresh.NeedsRefresh()
        {
            return featureEnable && NeedsRefresh();
        }

        bool xenwinsvc.IRefresh.Refresh(bool force)
        {
            if(!force)
                doAutoUpdate();
            return true;
        }

        protected override void Finish()
        {
            JoinAutoUpdate();
            base.Finish();
        }

        static volatile bool isUpdating = false;
        public static bool IsUpdating()
        {
            return isUpdating;
        }
    }

}
