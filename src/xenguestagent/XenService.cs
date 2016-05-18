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
using System.Diagnostics;
using System.Threading;
using System.Management;
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.VisualBasic;
using System.Configuration.Install;
using System.Reflection;
using XenGuestLib;

[assembly:AssemblyVersion(XenVersions.Version)]
[assembly:AssemblyFileVersion(XenVersions.Version)]
[assembly:AssemblyCompanyAttribute(XenVersions.BRANDING_manufacturerLong)]
[assembly:AssemblyProductAttribute(XenVersions.BRANDING_toolsForVMs)]
[assembly:AssemblyDescriptionAttribute(XenVersions.BRANDING_guestAgentLong)]
[assembly:AssemblyTitleAttribute(XenVersions.BRANDING_guestAgentLong)]
[assembly:AssemblyCopyrightAttribute(XenVersions.BRANDING_copyrightGuestAgent)]


namespace xenwinsvc
{
    class TimeDateTraceListener : Microsoft.VisualBasic.Logging.FileLogTraceListener
    {
        public TimeDateTraceListener(String name)
            : base(name)
        {
            Debug.Print("TD Trace");
            base.Append = true;
            base.AutoFlush = true;
            base.MaxFileSize = 1024 * 1024;
            base.LogFileCreationSchedule = Microsoft.VisualBasic.Logging.LogFileCreationScheduleOption.Daily;
            base.Location = Microsoft.VisualBasic.Logging.LogFileLocation.Custom;
            System.IO.Directory.CreateDirectory(Application.CommonAppDataPath);
            base.CustomLocation = Application.CommonAppDataPath;
            Debug.Print("Log location " + base.CustomLocation);

        }

        public override void WriteLine(object o)
        {
            base.WriteLine(DateTime.Now.ToString() +" : " + o.ToString());
        }
        public override void WriteLine(string message)
        {
            base.WriteLine(DateTime.Now.ToString() +" : " + message);
        }
    }
        
       /* protected override void OnStart(string[] args)
        {
            // Start thread - so we can do everything in the background
            TextWriterTraceListener tlog = new TimeDateTraceListener(Application.CommonAppDataPath + "\\GuestData.log", "GuestData");
            Trace.Listeners.Add(tlog);
            Trace.AutoFlush = true;
            Trace.WriteLine("OnStart");  
            InstallState = new InstallerState();
            installthread = new Thread(InstallThreadHandler);
            installthread.Start();
            
        }*/

    class XenService : System.ServiceProcess.ServiceBase, IExceptionHandler
    {

        TimeDateTraceListener tlog;
        public XenService()
        {
     
            Debug.Print("GuestAgent Init");
            tlog = new TimeDateTraceListener("guest");
            Trace.Listeners.Add(tlog); Trace.WriteLine("This is all");
            this.ServiceName = Branding.Instance.getString("BRANDING_guestAgent");
            this.CanStop = true;
            this.CanHandleSessionChangeEvent = true;
            this.CanHandlePowerEvent = true;
            this.AutoLog = true;
            servicestatelock = new object();
            Debug.Print("GuestAgent Init Done");
        }


        ManualResetEvent needsShutdown;
        Thread ServiceThread = null;
        WmiSession wmisession;


        uint cntr = 0;
        bool timeToRefresh()
        {
            if ((cntr % 26) == 0)
            {
                return true;
            }
            else
            {
                cntr++;
                return false;
            }
        }
        void resetTimeToRefresh()
        {
            cntr = 1;
        }

        void handleUnsuspended(object nothing, EventArrivedEventArgs args)
        {
            try
            {
                Refresher.RefreshAll(true);
            }
            catch (Exception e)
            {
                HandleException("Resume from suspend", e);
            }
        }        

        void ServiceThreadHandler()
        {
            try {
                Debug.Print("ServiceThreadHandler");
                needsShutdown.Reset();

                NetInfo.StoreChangedNetworkSettings();

                WmiBase.Reset();
                Debug.Print("WMI Check");
                if (WmiBase.Check())
                {
                    starting = true;
                    WmiCapableServiceThreadHandler();
                    starting = false;
                    running = true;
                }
                else
                {
                    running = false;
                    WaitHandle[] waitHandles = new WaitHandle[] 
                    {
                         (new WmiIncapableThread()).Incapable,
                         needsShutdown
                    };
                   
                    Debug.Print("Waiting for WMI capability to begin");
                    try
                    {
                        EventLog.WriteEntry(Branding.Instance.getString("BRANDING_errNoWMI"));
                    }
                    catch { };

                    int activehandle =WaitHandle.WaitAny(waitHandles);
                    Debug.Print("Received event");
       
                    if (activehandle == 0 ) {
                        try
                        {
                            EventLog.WriteEntry(Branding.Instance.getString("BRANDING_errNoWMI"));
                        }
                        catch { };
                        starting = true;
                        WmiCapableServiceThreadHandler();
                        starting = false;
                        running = true;
                    }
                }
            }
            catch (Exception e) {
                HandleException("Main Service Thread", e);
            }
        }

        class WmiIncapableThread {
            System.Threading.Timer timer;
            public WmiIncapableThread() {
                Incapable = new ManualResetEvent(false);
                timer = new System.Threading.Timer(WmiIncapableServiceThreadHandler, null, 4500, 4500);
            }
            public ManualResetEvent Incapable; 
            void WmiIncapableServiceThreadHandler(object nothing)
            {
                try {
                    WmiBase.Reset();
                    if (WmiBase.Check())
                    {
                        timer.Dispose();
                        Incapable.Set();
                    }
                }
                catch {
                     timer.Dispose();
                     Incapable.Set();
                }
            }

        }

        void RunProcess(string name, string arg, string comment)
        {
            try
            {

                Process myProcess = new Process();
                myProcess.StartInfo.UseShellExecute = false;
                myProcess.StartInfo.FileName = name;
                myProcess.StartInfo.Arguments = arg;
                myProcess.StartInfo.CreateNoWindow = true;
                myProcess.Start();
                myProcess.WaitForExit(5000);
            }
            catch (Exception e)
            {
                wmisession.Log("Process: unable to "+comment+"\n" + e.ToString());
            }
        }

        void WmiCapableServiceThreadHandler() 
        {
            try
            {
                wmisession = WmiBase.Singleton.GetXenStoreSession("Features");

                wmisession.Log("Guest Agent Starting");
                Refresher.Add(new PVInstallation(this));
               
                wmisession.Log("About to run apps");

                RunProcess("wmiadap","/f","refresh WMI ADAP");
                RunProcess("diskperf", "-y","enable disk perf counters");

                
                wmisession.Log("About to run features");
                new FeatureLicensed(this);
                new FeatureVSSLicensed(this);
                new FeatureDumpLog(this);
                new FeatureGC(this);
                new FeaturePing(this);
                new FeatureDomainJoin(this);
                new FeatureSetComputerName(this);
                new FeatureXSBatchCommand(this);
                new FeatureAutoUpdate(this);

                wmisession.Log("About to try snapshot");
                if (FeatureSnapshot.IsSnapshotSupported())
                {
                    Refresher.Add(new FeatureSnapshot(this));
                }
                else
                {
                    wmisession.Log("Snapshot not supported on this platform");
                    FeatureSnapshot.removeSnapshot(wmisession);
                }
                new FeatureTerminalServicesReset(this);
                new FeatureTerminalServices(this);
                new FeatureStaticIpSetting(this);
                wmisession.Log("About to add refreshers");
                
                Refresher.Add(new NetInfo(this));
                Refresher.Add(new VolumeInfo());
                Refresher.Add(new MemoryInfo());
                Refresher.Add(new XenAppSessionInfo());

                wmisession.Log("About to add handlers ");
                clipboardhandler = new ClipboardManager(this);
                Disposer.Add(clipboardhandler);
                clipboardhandler.Run();
                Disposer.Add(WmiBase.Singleton.ListenForEvent("CitrixXenStoreUnsuspendedEvent", new EventArrivedEventHandler(handleUnsuspended)));

                Refresher.RefreshAll(true);
                wmisession.Log("running ");
                Refresher.Run(this);

            }
            catch (Exception e)
            {
                HandleException("Service handler", e);
            }
        }

        bool running;
        bool starting;
        object servicestatelock;

        void ServiceThreadShutdown()
        {
            try {
                try {
                    Refresher.Dispose();
                    Disposer.Dispose();
                }
                catch (Exception ex) {
                    wmisession.Log("Errors disposing of threads: "+ex.ToString());
                    throw;
                }
    
                wmisession.Log("Guest Agent Stopped");
                try
                {
                    wmisession.Dispose();
                }
                catch { }
            }
            catch {}
            finally {
                WmiBase.Reset();
            }
        }

        private Boolean resetting = false;
        public void OnNeedsReset()
        {
            Debug.Print("Reset requested");
            if (!resetting)
            {
                try
                {
                    Debug.Print("I need to be reset");
                    lock (servicestatelock)
                    {
                        resetting = true;
                        Debug.Print("Got lock");
                        
                        if (starting || running)
                        {

                            Debug.Print("Running, should shutdown");
                            ServiceThreadShutdown();
                            starting = false;
                            running = false;
                            Debug.Print("shutdown done");
                        }
                        if (ServiceThread != null)
                        {
                            needsShutdown.Set();
                            //If we have got here from an exception raised in the ServiceThread, then the 
                            //service thread is already over, so no need to join
                            if (!ServiceThread.Equals(Thread.CurrentThread)) {
                                ServiceThread.Join();
                            }
                            ServiceThread = null;
                        }
                        
                        Debug.Print("Starting new thread");
                        resetting = false;
                        ServiceThread = new Thread(ServiceThreadHandler, 256*1024 );
                        ServiceThread.Start();
                        Debug.Print("Running");
                    }
                }
                catch (Exception e)
                {
                    Debug.Print("OnNeedsReset :" + e.ToString());
                    EventLog.WriteEntry("Reset Failed: "+e.ToString() );
                }
            }
        }


        public void HandleException(string from, Exception e)
        {

            try
            {
                if (e is ManagementException)
                {
                    if (WmiBase.HandleManagementException(e as ManagementException))
                    {
                        Debug.Print("WMI objects cannot be located.  Resetting service");
                        OnNeedsReset();
                        return;
                    }
                }
                wmisession.Log("Unhandled exception from "+from+".  Resetting service:\n" + e.ToString());
                wmisession.Log("Dumping stored debug messages");
                WmiBase.Singleton.DumpDebugMsg();
            }
            catch { 
            
            }
            try
            {
                OnNeedsReset();
            }
            catch
            {
                Debug.Print("Reset failed");
            }
        }

        protected override bool OnPowerEvent(System.ServiceProcess.PowerBroadcastStatus powerStatus)
        {
            switch (powerStatus)
            {
                case System.ServiceProcess.PowerBroadcastStatus.ResumeAutomatic:
                    OnNeedsReset();
                    break;
                case System.ServiceProcess.PowerBroadcastStatus.ResumeCritical:
                    OnNeedsReset();
                    break;
                case System.ServiceProcess.PowerBroadcastStatus.ResumeSuspend:
                    OnNeedsReset();
                    break;
                default:
                    break;
            }
            return true;
        }

        ClipboardManager clipboardhandler;
        protected override void OnSessionChange(System.ServiceProcess.SessionChangeDescription changeDescription)
        {
            try {
                WmiBase.Singleton.DebugMsg("Session Change");
                lock (servicestatelock) { 
                    if (running)
                    {
                        clipboardhandler.HandleSessionChange(changeDescription.Reason);
                    }
                }
                base.OnSessionChange(changeDescription);
            }
            catch (Exception e) {
                HandleException("Session Change Handler", e);
            }
        }
        protected override void OnStart(string[] args)
        {
            try {
                Debug.Print("Starting");
                try {
                    EventLog.WriteEntry("Service starting");
                }
                catch {
                    Debug.Print("Writing to the event log is failing");
                }
                needsShutdown = new ManualResetEvent(false);
                base.OnStart(args);
                hiddenform = new HiddenForm();
                new Thread(RunMessagePump, 1024*256).Start();
                hiddenform.started.WaitOne();
                starting = false;
                running = false;
                OnNeedsReset();
            }
            catch (Exception e) {
                Debug.Print("Exception :" + e.ToString());
                try {
                    EventLog.WriteEntry("Exception :"+e.ToString());
                }
                catch{}
            }

        }

        protected override void OnStop()
        {
            EventLog.WriteEntry("Service stopping");
            Debug.Print("Stopping");
            lock (servicestatelock)
            {
                try
                {
                    EventLog.WriteEntry("Service stopping have lock");
                }
                catch { }
                Debug.Print("Stopping, have lock");
                resetting = true;
                if (starting || running)
                {
                    try
                    {
                        EventLog.WriteEntry("Service stopping shutting down");
                    }
                    catch { }
                    Debug.Print("Shutting down");
                    ServiceThreadShutdown();
                    starting = false;
                    running = false;
                }
                try
                {
                    EventLog.WriteEntry("Service stopping nothing running");
                }
                catch { }
                Debug.Print("Nothing running");
                if (ServiceThread != null)
                {
                    try
                    {
                        EventLog.WriteEntry("Service stopping joining service thread");
                    }
                    catch { }
                    Debug.Print("Joining servicethread");
                    needsShutdown.Set();
                    ServiceThread.Join();
                    ServiceThread = null;
                }
            }
            try
            {
                EventLog.WriteEntry("Service stopping done");
            }
            catch { }
            Debug.Print("Stopping essentially done");
            hiddenform.Dispose();
            Application.Exit();
            base.OnStop();

        }

        protected override void OnShutdown()
        {
            this.OnStop();
            base.OnShutdown();
        }

        static void Install(bool undo, string[] args)
        {
            using (AssemblyInstaller asminstall = new AssemblyInstaller(typeof(XenService).Assembly, args))
            {
                System.Collections.IDictionary state = new System.Collections.Hashtable();
                asminstall.UseNewContext = true;
                try
                {
                    if (undo)
                    {
                        asminstall.Uninstall(state);
                    }
                    else
                    {
                        asminstall.Install(state);
                        asminstall.Commit(state);
                    }
                }
                catch
                {
                    try
                    {
                        asminstall.Rollback(state);
                    }
                    catch { }
                }
            }
        }

        public static int Main(string[] args)
        {
            bool rethrow = false;
            try
            {
                bool install = false;
                bool uninstall = false;

                foreach (string arg in args)
                {

                    switch (arg)
                    {
                        case "-i":
                        case "-install":
                            install = true;
                            break;
                        case "-u":
                        case "-uninstall":
                            uninstall = true;
                            break;
                        default:
                            break;
                    }
                }
                if (uninstall)
                {
                    Install(true, args);
                }
                if (install)
                {
                    Install(false, args);
                }
                if (!(uninstall || install))
                {
                    Debug.Print("Service Main");
                    rethrow = true;
                    System.ServiceProcess.ServiceBase.Run(new XenService());
                    rethrow = false;
                    Debug.Print("Service Main Done");
                }
                return 0;
            }
            catch (Exception e)
            {
                if (rethrow) throw;
                Console.Error.WriteLine(e.ToString());
                return -1;
            }
            
            
        }
        HiddenForm hiddenform;
        void RunMessagePump()
        {
            try
            {
                Application.Run(hiddenform);
            }
            catch (Exception e)
            {
                wmisession.Log("Message Handler Unhandled Exception\n" + e.ToString());
            }
        }
    }

    // HiddenForm is provided to act as a message pump for events that require one behind the scenese
    public class HiddenForm : Form
    {
        public ManualResetEvent started;

        public HiddenForm()
        {
            started = new ManualResetEvent(false);
            InitializeComponent();
        }


        private void HiddenForm_Load(object sender, EventArgs e)
        {
            started.Set();
        }

        private void HiddenForm_FormClosing(object sender, FormClosingEventArgs e)
        {
        }

        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(0, 0);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "HiddenForm";
            this.Text = "HiddenForm";
            this.WindowState = System.Windows.Forms.FormWindowState.Minimized;
            this.Load += new System.EventHandler(this.HiddenForm_Load);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.HiddenForm_FormClosing);
            this.ResumeLayout(false);
        }
    }
}
