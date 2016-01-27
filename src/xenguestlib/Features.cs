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
using System.Runtime.InteropServices;
using NetFwTypeLib;

namespace xenwinsvc
{
    public interface IExceptionHandler
    {
        void HandleException(string from, Exception e);
    }

    public class Disposer
    {
        static object disposelock = new object();
        static Stack<IDisposable> disposees = new Stack<IDisposable>();
        static public void Add(IDisposable disposee)
        {
            lock (disposelock) {
                if (!disposees.Contains(disposee)) {
                    disposees.Push(disposee);
                }
            }
        }
        static public void Dispose() {
            lock (disposelock) {
                while (disposees.Count > 0) {
                    try {
                        disposees.Pop().Dispose();
                    }
                    catch (Exception e) {
                        Debug.Print("Disposal error: "+e.ToString());
                    }
                }
            }
        }
    }

    public abstract class Feature : IDisposable
    {
        static List<Feature> features = new List<Feature>();
        protected WmiSession wmisession;
        string name;
        protected XenStoreItem advert = null;
        protected bool enabled = false;
        public static void Advertise(WmiSession wmisession)
        {
            foreach (Feature feature in features)
            {
                feature.doAdvert();
            }
                
        }
        protected void addAdvert(string advertname) {
            advert = wmisession.GetXenStoreItem(advertname);
            features.Add(this);
        }
        protected void doAdvert() {
            try {
                advert.value = "1";
                
            }
            catch (System.Management.ManagementException e) {
                enabled = false;
                if (e.ErrorCode == ManagementStatus.AccessDenied) {
                    wmisession.Log("Failed to advertise "+name+" (feature disabled)");
                }
                else {
                        throw e;
                }
            }
        }
        protected XenStoreItem controlKey=null;
        WmiWatchListener listener = null;
        IExceptionHandler exceptionhandler;
        bool controlmustexist = true;
        public Feature(string name, string advertise, string control, bool controlmustexist, IExceptionHandler exceptionhandler)
        {
            this.exceptionhandler = exceptionhandler;
            this.name = name;
            wmisession = WmiBase.Singleton.GetXenStoreSession("Citrix Xen Service Feature : " + name);
            wmisession.Log("New Feature");
            controlKey = wmisession.GetXenStoreItem(control);
            this.controlmustexist = controlmustexist;
            try
            {
                if (controlKey.value != "")
                {
                    wmisession.Log("Control key "+control+":"+controlKey.value);
                }
            }
            catch {}
            enabled = true;
            listener = controlKey.Watch(new EventArrivedEventHandler(onFeatureWrapper));
            if (!advertise.Equals(""))
            {
                this.addAdvert(advertise);
            }
            Disposer.Add(this);
         
        }
        protected abstract void onFeature();
        void onFeatureWrapper(object nothing, EventArrivedEventArgs args)
        {
            try
            {
                if (enabled && ((!controlmustexist) || controlKey.Exists())) {
                    onFeature();
                }
            }
            catch (System.Management.ManagementException e) {
                enabled = false;
                if (e.ErrorCode == ManagementStatus.AccessDenied) {
                    wmisession.Log("Feature "+name+" disabled");
                }
                else {
                    throw e;
                }
            }
            catch (Exception e)
            {
                enabled = false; //Don't want to call the feature if it causes an exception
                exceptionhandler.HandleException("Feature " + name, e);
            }
        }

        protected virtual void Finish()
        {
            if (advert != null) {
                if (advert != null)
                {
                    try
                    {
                        advert.Remove();
                    }
                    catch { };
                }
                features.Remove(this);
                advert = null;
            }
            if (listener != null) {
                listener.Dispose();
                listener = null;
            }

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
        ~Feature()
        {
            Dispose(false);
        }

    }

    public class FeatureGC : Feature {
        public FeatureGC(IExceptionHandler exceptionhandler) : base("GC", "", "control/garbagecollect", true, exceptionhandler) { }
        override protected void onFeature()
        {
            GC.Collect();
            controlKey.Remove();
        }

    }

    public class FeaturePing : Feature
    {
        public FeaturePing(IExceptionHandler exceptionhandler) : base("Ping", "", "control/ping", true, exceptionhandler) { }
        override protected void onFeature()
        {
            controlKey.Remove();
        }

    }

    public class FeatureDumpLog : Feature
    {
        public FeatureDumpLog(IExceptionHandler exceptionhandler) : base("Dump Log", "", "control/dumplog", true, exceptionhandler) { }
        override protected void onFeature()
        {
            try {
                WmiBase.Singleton.DumpDebugMsg();
            }
            catch {
                Debug.Print("Failed to DumpLog");
            }
            try {
                controlKey.Remove();
            }
            catch {
                Debug.Print("Failed to remove dump log control key");
            }
        }
    }

    // To use SetComputername Feature
    //
    // (all keys are relative to /domain/local/xxx/control/setcomputername/)
    //
    // Take a transaction
    // Check that feature-setcomputername exists else, drop transation
    // if state is set, or action is set, drop transaction,
    //   wait, then try again
    // if you want to set a computer name other than the xapi name for the domain, 
    //   set computername to the desited name (otherwise the xapi name for the
    //   domain will be used as a default) 
    // set action to "set"
    // commit the transaction
    // read the value of state, it should read either
    //   InProgress
    //   NoChange
    //   SucceededNeedsReboot
    //   Failed
    // while state reads InProgress, wait and reread the value of state 
    //   until it reads either Failed or Succeded
    // once the action has completed, the action key will be removed
    // in the event state reads Failed, error will contain some info
    //   which may be usable
    // in other cases, 
    // if state reads NoChange, the VM was already set to the desired name
    // if the state reads SucceededNeedsReboot, the rename succeded, and the
    //   VM must be rebooted for the change to take effect
    // you should remove the state entry if finished and not rebooting to allow
    //   other applications the opportunity to change the computer name.

    public class FeatureSetComputerName : Feature
    {
        XenStoreItem name;
        XenStoreItem state;
        XenStoreItem error;
        XenStoreItem warn;

        public FeatureSetComputerName(IExceptionHandler exceptionhandler)
            : base(Branding.Instance.getString("BRANDING_setComputerName"), "control/feature-setcomputername", "control/setcomputername/action", true, exceptionhandler)
        {
            name =  wmisession.GetXenStoreItem("control/setcomputername/name");
            state =  wmisession.GetXenStoreItem("control/setcomputername/state");
            error = wmisession.GetXenStoreItem("control/setcomputername/error");
            warn = wmisession.GetXenStoreItem("control/setcomputername/warn");
        }
        
        void SetComputerName() {

            wmisession.Log("Set Computer Name Requested");
            state.value = "InProgress";
            String defaultname;
            bool res;

            try {
                defaultname = name.value;
                name.Remove();
            }
            catch {
                try {
                    wmisession.Log("Setting computer name to default");
                    XenStoreItem name = wmisession.GetXenStoreItem("name");
                    defaultname = name.value;
                    
                }
                catch (Exception e){
                    wmisession.Log("Unable to read default name for domain from xenstore: "+ e.ToString());
                    error.value = "Can't read default computer name";
                    state.value = "Failed";
                    return;
                }
            }

            if (defaultname.Equals("")) {
                wmisession.Log("Can't set to empty computer name");
                error.value = "Computer name empty";
                state.value = "Failed";
                return;
            }

            if (defaultname.Length > Win32Impl.MAX_COMPUTERNAME_LENGTH)
            {
                warn.value = "Computer name exceeds MAX_COMPUTERNAME_LENGTH.  The NetBIOS name will be truncated";
            }

            try
            {
                wmisession.Log("Setting computer name to "+defaultname);
                Win32Impl.SetLastError(0);
                res = Win32Impl.SetComputerNameEx(Win32Impl.COMPUTER_NAME_FORMAT.ComputerNamePhysicalDnsHostname, defaultname);
                if (!res) {
                    wmisession.Log("Setting computer name failed " + Marshal.GetLastWin32Error().ToString());
                    error.value = "Setting name failed (error code "+Marshal.GetLastWin32Error().ToString()+")";
                    state.value = "Failed";
                    return;
                }
                wmisession.Log("Setting computer name succceded");
            }
            catch(Exception e)
            {
                wmisession.Log("Exception setting computer name : " + e.ToString());
                error.value = "Exception calling set computer name";
                state.value = "Failed";
                return;
            }
            try
            {
                wmisession.Log("Target hostname " + defaultname);
                wmisession.Log("Current hostname" + Win32Impl.GetComputerDnsHostname());
                if (defaultname.Equals(Win32Impl.GetComputerDnsHostname()))
                {
                    wmisession.Log("No need to reboot to change computer name, already " + defaultname);
                    state.value = "NoChange";
                    return;
                }
            }
            catch{
            }
            state.value = "SucceededNeedsReboot";
        }


        override protected void onFeature()
        {
            if (controlKey.Exists() && !state.Exists()) {
                try {
                    if (error.Exists()) {
                        error.Remove();
                    }
                    if (warn.Exists())
                    {
                        warn.Remove();
                    }

                    if (state.Exists())
                    {
                        error.value = "Setting name already in progress";
                        state.value = "Failed";
                        return;
                    }
                    if (controlKey.value.Equals("set")) {
                        SetComputerName();
                    }
                    else {
                        error.value = "Unknown action : " + controlKey.value;
                        state.value = "Failed";
                    }
                }
                catch (Exception e) {
                    if (!error.Exists()) {
                        error.value=e.ToString();
                    }
                    state.value="Failed";
                }
                finally {
                    // We always want to remove the controlKey, so that
                    // it can be set again
                    controlKey.Remove();
                }
            }
        }
    }



    // To use DomainJoin Feature
    //
    // (all keys are relative to /domain/local/xxx/domainjoin/)
    //
    // Take a transaction
    // if state is set, or action is set, drop transaction,
    //   wait, then try again
    // set domainname to desired domain
    // set user and password to a user on said domain with rights to
    //   add you to the domain
    // set action to either joindomain or unjoindomain as appropriate
    // commit the transaction
    // read the value of state, it should read either
    //   InProgress
    //   Succeeded
    //   Failed
    // while state reads InProgress, wait and reread the value of state 
    //   until it reads either Failed or Succeded
    // in the event it reads Failed, error will contain some info
    //   which may be usable
    // remove the state entry
    //

    public class FeatureDomainJoin : Feature
    {
        XenStoreItem domainName;
        XenStoreItem userName;
        XenStoreItem password;
        XenStoreItem state;
        XenStoreItem error;

        public FeatureDomainJoin(IExceptionHandler exceptionhandler)
            : base("Domain Join", "", "control/domainjoin/action", true, exceptionhandler)
        {
            domainName = wmisession.GetXenStoreItem("control/domainjoin/domainname");
            userName = wmisession.GetXenStoreItem("control/domainjoin/user");
            password = wmisession.GetXenStoreItem("control/domainjoin/password");
            state = wmisession.GetXenStoreItem("control/domainjoin/state");
            error = wmisession.GetXenStoreItem("control/domainjoin/error");
        }

        void JoinDomain()
        {
            state.value = "InProgress";
            ManagementObject cs = WmiBase.Singleton.Win32_ComputerSystem;
            ManagementBaseObject mb = cs.GetMethodParameters("JoinDomainOrWorkgroup");
            mb["Name"] = domainName.value;
            mb["Password"] = password.value;
            mb["UserName"] = domainName.value + "\\" + userName.value;
            mb["AccountOU"] = null;
            mb["FJoinOptions"] = (UInt32)1;
            ManagementBaseObject outParam = cs.InvokeMethod("JoinDomainOrWorkgroup", mb, null);
            if ((UInt32)outParam["returnValue"] == 0)
            {
                state.value = "Succeeded";
            }
            else
            {
                error.value = "" + (UInt32)outParam["returnValue"];
                state.value = "Failed";
            }
        }

        void UnjoinDomain()
        {
            state.value = "InProgress";
            ManagementObject cs = WmiBase.Singleton.Win32_ComputerSystem;
            ManagementBaseObject mb = cs.GetMethodParameters("UnjoinDomainOrWorkgroup");
            mb["Password"] = password.value;
            mb["UserName"] = domainName.value + "\\" + userName.value;
            mb["FUnjoinOptions"] = (UInt32)0;
            ManagementBaseObject outParam = cs.InvokeMethod("UnjoinDomainOrWorkgroup", mb, null);
            if ((UInt32)outParam["returnValue"] == 0)
            {
                state.value = "Succeeded";
            }
            else
            {
                error.value = "" + (UInt32)outParam["returnValue"];
                state.value = "Failed";
            }
        }


        override protected void onFeature()
        {
            if (controlKey.Exists() && !state.Exists())
            {
                try
                {
                    if (error.Exists())
                    {
                        error.Remove();
                    }
                    if (!domainName.Exists())
                    {
                        error.value = "domainname must be specified";
                        throw new Exception("domainname must be specified");
                    }
                    if (!userName.Exists())
                    {
                        error.value = "username must be specified";
                        throw new Exception("username must be specified");
                    }
                    if (!password.Exists())
                    {
                        error.value = "password must be specified";
                        throw new Exception("password must be specified");
                    }
                    if (controlKey.value.Equals("joindomain"))
                    {
                        JoinDomain();
                    }
                    else if (controlKey.value.Equals("unjoindomain"))
                    {
                        UnjoinDomain();
                    }
                    // If completed, remove the arguments, to avoid
                    // them hanging around in xenstore.  
                    domainName.Remove();
                    userName.Remove();
                    password.Remove();
                }
                catch (Exception e)
                {
                    if (!error.Exists())
                    {
                        error.value = e.ToString();
                    }
                    state.value = "Failed";
                }
                finally
                {
                    // We always want to remove the controlKey, so that
                    // it can be set again
                    controlKey.Remove();
                }
            }
        }
    }
    public class FeatureTerminalServicesReset : Feature {
        XenStoreItem datats;
        public FeatureTerminalServicesReset(IExceptionHandler exceptionhandler)
            : base("Terminal Services Reset", "control/feature-ts2", "data/ts", false, exceptionhandler)
        {
            datats = wmisession.GetXenStoreItem("data/ts");
            Disposer.Add(WmiBase.Singleton.ListenForEvent("__InstanceModificationEvent", new EventArrivedEventHandler(onFeatureWrapper)));
            onFeature();
        }

        void onFeatureWrapper(object nothing, EventArrivedEventArgs args)
        {
            ManagementBaseObject targetInstance = (ManagementBaseObject)args.NewEvent["TargetInstance"];
            datats.value = (uint)targetInstance.Properties["AllowTSConnections"].Value != 0 ? "1" : "0";
            wmisession.Log("Setting data/ts to " + datats.value);
        }

        bool query()
        {
            try
            {
                ManagementObject termserv = WmiBase.Singleton.Win32_TerminalServiceSetting;
                return (((uint)termserv["AllowTSConnections"]) != 0);
            }
            catch
            {
                wmisession.Log("Terminal services not found");
                return false;
            }
        }
        protected override void onFeature()
        {
            if (!datats.Exists())
            {
                bool enabled = query();
                string newtsvalue = (enabled ? "1" : "0");
                wmisession.Log("Setting data/ts to " + newtsvalue);
                datats.value = newtsvalue;
            }
        }
    }

    public class FeatureTerminalServices : Feature
    {
        XenStoreItem datats;
        public FeatureTerminalServices(IExceptionHandler exceptionhandler)
            : base("Terminal Services", "control/feature-ts", "control/ts", true, exceptionhandler)
        {
            datats = wmisession.GetXenStoreItem("data/ts");
        }
        void ChangeFirewallException(bool Enable)
        {
            try
            {
                Type type = Type.GetTypeFromCLSID(new Guid("{304CE942-6E39-40D8-943A-B913C40C9CD4}"));
                INetFwMgr fwMgr = (INetFwMgr)Activator.CreateInstance(type);
                INetFwService services = fwMgr.LocalPolicy.CurrentProfile.Services.Item(NET_FW_SERVICE_TYPE_.NET_FW_SERVICE_REMOTE_DESKTOP);
                services.Enabled = Enable;
            }
            catch {
                    wmisession.Log("Cannot modify Firewall RDP setting");
            }
        }

        void set(bool enable)
        {
            try {
                ManagementObject termserv = WmiBase.Singleton.Win32_TerminalServiceSetting;
                ManagementBaseObject mb = termserv.GetMethodParameters("SetAllowTSConnections");
                mb["AllowTSConnections"] = (uint)(enable ? 1 : 0);
                mb["ModifyFirewallException"] = 1;
                ChangeFirewallException(enable);
                termserv.InvokeMethod("SetAllowTSConnections", mb, null);
            }
            catch {
                wmisession.Log("Terminal Services not found");
            }
        }

        protected override void onFeature()
        {
            if (controlKey.Exists())
            {
                string enable;

                enable = controlKey.value;

                controlKey.Remove();
                int enableval;
                try
                {
                    enableval = int.Parse(enable);
                }
                catch
                {
                    return;
                }

                this.set(enableval != 0);
            }

        }
    }

    public class FeatureXSBatchCommand : Feature
    {
        XenStoreItem    state;
        XenStoreItem    script;
        XenStoreItem    ret;
        XenStoreItem    stdout;
        XenStoreItem    stderr;
        int             returnCode;
        string          stdoutStr;
        string          stderrStr;
        string          batchFile   = "";
        object          cmdLock     = new object();
        bool            newCommand;

        const string    READY       = "READY";
        const string    IN_PROGRESS = "IN PROGRESS";
        const string    FAILURE     = "FAILURE";
        const string    TRUNCATED   = "TRUNCATED";
        const string    SUCCESS     = "SUCCESS";
        const int       MAXLENGTH   = 1024;
        
        public FeatureXSBatchCommand(IExceptionHandler exceptionhandler) :
            base("XS Batch Command", "", "control/batcmd/state", true, exceptionhandler)
        {
            if (wmisession.GetXenStoreItem("control/feature-remote-exec").Exists()) {
                wmisession.Log("Remote exec found");
                this.Dispose();
                throw new Exception("remote-exec exists");
            }
            
            this.state = wmisession.GetXenStoreItem("control/batcmd/state");
            this.script = wmisession.GetXenStoreItem("control/batcmd/script");
            this.ret = wmisession.GetXenStoreItem("control/batcmd/return");
            this.stdout = wmisession.GetXenStoreItem("control/batcmd/stdout");
            this.stderr = wmisession.GetXenStoreItem("control/batcmd/stderr");
            this.addAdvert("control/feature-xs-batcmd");
        }

        delegate void TransactionCode();

        void handleTransaction(TransactionCode tc)
        {
            bool handled = false;
            while (!handled)
            {
                wmisession.StartTransaction();
                try
                {
                    tc();
                    try
                    {
                        wmisession.CommitTransaction();
                        handled = true;
                    }
                    catch
                    {
                        // an exception during the commit means handled doesn't get set
                    }
                }
                catch (Exception e)
                {
                    throw e;
                }
                finally
                {
                    if (!handled)
                    {
                        wmisession.AbortTransaction();
                    }
                }
            }
        }

        void runCommand(string batchfile)
        {
            string tmpDir = System.IO.Path.GetRandomFileName();
            int availBytes;

            stderrStr = "";
            stdoutStr = "";

            System.Security.AccessControl.DirectorySecurity sec = new System.Security.AccessControl.DirectorySecurity();

            sec.AddAccessRule(
                new System.Security.AccessControl.FileSystemAccessRule(
                    System.Security.Principal.WindowsIdentity.GetCurrent().User,
                    System.Security.AccessControl.FileSystemRights.FullControl,
                    System.Security.AccessControl.AccessControlType.Allow));

            System.Security.AccessControl.FileSecurity filesec = new System.Security.AccessControl.FileSecurity();
            filesec.AddAccessRule(
                new System.Security.AccessControl.FileSystemAccessRule(
                    System.Security.Principal.WindowsIdentity.GetCurrent().User,
                    System.Security.AccessControl.FileSystemRights.FullControl,
                    System.Security.AccessControl.AccessControlType.Allow));

            System.IO.Directory.CreateDirectory(tmpDir, sec);

            using (System.IO.StreamWriter execstream = new System.IO.StreamWriter(System.IO.File.Create(tmpDir+"\\exec.bat",1024, System.IO.FileOptions.None, filesec)))
            {
                execstream.Write(batchfile);
            }
            
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(Environment.GetEnvironmentVariable("SystemRoot") + "\\System32\\cmd.exe", "/Q /C " + tmpDir + "\\exec.bat")
                {
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                };

                using (Process proc = new Process())
                {
                    proc.StartInfo = startInfo;
                    
                    proc.OutputDataReceived += 
                        delegate(object sendingProcess, DataReceivedEventArgs outline) {
                            if (!String.IsNullOrEmpty(outline.Data))
                            {
                                lock (stdoutStr)
                                {
                                    availBytes = MAXLENGTH - stdoutStr.Length;
                                    stdoutStr += (availBytes >= outline.Data.Length) ? outline.Data : outline.Data.Substring(0, availBytes);
                                }
                            }
                        };
                    
                    proc.ErrorDataReceived += 
                        delegate(object sendingProcess, DataReceivedEventArgs outline) {
                            if (!String.IsNullOrEmpty(outline.Data))
                            {
                                lock (stderrStr)
                                {
                                    availBytes = MAXLENGTH - stderrStr.Length;
                                    stderrStr += (availBytes >= outline.Data.Length) ? outline.Data : outline.Data.Substring(0, availBytes);
                                }
                            }
                        };

                    proc.Start();
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    proc.WaitForExit();
                    
                    returnCode = proc.ExitCode;
                }
            }
            finally
            {
                System.IO.Directory.Delete(tmpDir,true);
            }
        }


        protected override void onFeature()
        {
            string result = FeatureXSBatchCommand.SUCCESS;
            try
            {
                lock (cmdLock)
                {
                    newCommand = false;
                    handleTransaction(
                        delegate()
                        {
                            if (controlKey.Exists())
                            {
                                if (controlKey.value.Equals(FeatureXSBatchCommand.READY))
                                {

                                    this.state.value = FeatureXSBatchCommand.IN_PROGRESS;
                                    batchFile = this.script.value;
                                    newCommand = true;
                                }
                            }
                        });
                    if (!newCommand)
                    {
                        return;
                    }
                }

                if ((int)Registry.GetValue("HKEY_LOCAL_MACHINE\\Software\\Citrix\\Xentools", "NoRemoteExecution", 0)!=0) {
                    this.stderr.value = "VM Blocked Remote Execution By Registry Key";
                    result = FeatureXSBatchCommand.FAILURE;
                    state.value = result;
                    return;
                }

                runCommand(batchFile);

                this.ret.value = returnCode.ToString();

                if (stderrStr.Length > FeatureXSBatchCommand.MAXLENGTH)
                {
                    stderrStr = stderrStr.Substring(0, FeatureXSBatchCommand.MAXLENGTH - 1);
                    result = FeatureXSBatchCommand.TRUNCATED;
                }

                if (stdoutStr.Length > FeatureXSBatchCommand.MAXLENGTH)
                {
                    stdoutStr = stdoutStr.Substring(0, FeatureXSBatchCommand.MAXLENGTH - 1);
                    result = FeatureXSBatchCommand.TRUNCATED;
                }

                this.stdout.value = stdoutStr;
                this.stderr.value = stderrStr;
                state.value = result;
            }
            catch (Exception e)
            {
                try
                {
                    wmisession.Log("Exception " + e.ToString());
                    this.stderr.value = (e.ToString().Length < FeatureXSBatchCommand.MAXLENGTH) ? e.ToString() : e.ToString().Substring(0, FeatureXSBatchCommand.MAXLENGTH);
                }
                finally
                {
                    result = FeatureXSBatchCommand.FAILURE;
                    state.value = result;
                }
            }
        }
    }

    public class FeatureLicensed : Feature
    {
        public FeatureLicensed(IExceptionHandler exceptionhandler)
            : base("licensed", "", "/guest_agent_features/Guest_agent_auto_update/licensed", false, exceptionhandler)
        {
        }
        static volatile bool licensed = true;
        public static bool IsLicensed()
        {
            return licensed;
        }
        protected override void onFeature()
        {
            int enableval = 1;
            if (controlKey.Exists())
            {
                try
                {
                    enableval = int.Parse(controlKey.value);
                }
                catch
                {
                    return;
                }
            }
            licensed = (enableval != 0);

            wmisession.Log("license status is " + licensed.ToString());
        }
    }

    public class FeatureVSSLicensed : Feature
    {
        public FeatureVSSLicensed(IExceptionHandler exceptionhandler)
            : base("VSSlicensed", "", "/VSS/licensed", false, exceptionhandler)
        {
        }
        static volatile bool licensed = true;
        public static bool IsLicensed()
        {
            return licensed;
        }
        protected override void onFeature()
        {
            int enableval = 1;
            if (controlKey.Exists())
            {
                try
                {
                    enableval = int.Parse(controlKey.value);
                }
                catch
                {
                    return;
                }
            }
            licensed = (enableval != 0);

            wmisession.Log("VSS license status is " + licensed.ToString());
        }
    }
}