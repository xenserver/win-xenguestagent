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
using System.Globalization;
using System.Diagnostics;
using System.Threading;
using System.Management;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using XenGuestLib;

namespace xenwinsvc
{

    public class ClipboardManager : IDisposable
    {

        public ClipboardManager(IExceptionHandler exceptionhandler)
        {
            this.exceptionhandler = exceptionhandler;
            WorkerProcess.AddToXDIgnoreApplicationList();
        }

        class ClipboardAccess : IDisposable
        {

            string currentclipboard="";
            bool currentclipboardchanged = false;
            string totalclipboard = "";
            string totalclientclipboard = null;
            WmiSession wmisession;
            XenStoreItem xsSetClipboard;
            XenStoreItem xsReportClipboard;
            WmiWatchListener serverwatch;
            WmiWatchListener clientwatch;

            public ClipboardAccess(WmiSession wmisession)
            {
                this.wmisession = wmisession;
                xsSetClipboard = wmisession.GetXenStoreItem("data/set_clipboard");
                xsReportClipboard = wmisession.GetXenStoreItem("data/report_clipboard");
                serverwatch = xsSetClipboard.Watch(new EventArrivedEventHandler(OnServerClipboard));
                clientwatch = xsReportClipboard.Watch(new EventArrivedEventHandler(OnClientClipboard));
            }

            void onClientClipboard()
            {
                if (xsReportClipboard.Exists()) {
                    string newclipboard = xsReportClipboard.value;
                }
                else
                {
                    // There is nothing set
                    if (totalclientclipboard != null)
                    {
                        string tempclip;
                        if (totalclientclipboard.Length > 1024)
                        {
                            tempclip = totalclientclipboard.Substring(0, 1024);
                            totalclientclipboard = totalclientclipboard.Substring(1024, totalclientclipboard.Length - 1024);
                        }
                        else
                        {
                            tempclip = totalclientclipboard;
                            totalclientclipboard = "";
                        }

                        xsReportClipboard.value = tempclip;
                        if (tempclip.Equals(""))
                        {
                            if (currentclipboardchanged)
                            {
                                currentclipboardchanged = false;
                                totalclientclipboard = currentclipboard;
                            }
                            else
                            {
                                totalclientclipboard = null;
                            }
                        }
                    }
                }
            }



            public void OnClientClipboard(object sender, EventArrivedEventArgs e)
            {
                try {
                    onClientClipboard();
                }
                catch (Exception ex) {
                    WmiBase.Singleton.DebugMsg("Client Clipboard Exception: "+ex.ToString());
                }
            }

            private object deprivLock = new object();

            IDeprivClient deprivClient = null;

            public void RegisterClient(IDeprivClient Client)
            {
                lock (deprivLock)
                {
                    this.deprivClient = Client;
                }
            }
            public void UnregisterClient()
            {
                lock (deprivLock)
                {
                    this.deprivClient = null;
                }
            }

            public void PushClientClipboard() {
                if (!currentclipboard.Equals(""))
                {
                    setClientClipboard(currentclipboard);
                }
                onServerClipboard();
            }

            void setClientClipboard(string value)
            {
                lock (deprivLock)
                {
                    if (deprivClient != null)
                    {
                        deprivClient.SetClipboard(value);
                    }
                }
            }

            void onServerClipboard()
            {
                string newclipboard = null;
                try
                {
                    if (xsSetClipboard.Exists())
                    {
                        newclipboard = xsSetClipboard.value;
                        Debug.Print("get new clipboard " + newclipboard);
                        xsSetClipboard.Remove();
                    }
                    else
                    {
                        return;
                    }
                }
                catch 
                {
                    return;
                }

                if (newclipboard == null)
                {
                    currentclipboard = totalclipboard;
                    setClientClipboard(totalclipboard);
                    totalclipboard = "";
                }
                else
                {
                    totalclipboard += newclipboard;
                }

            }
            public void OnServerClipboard(object sender, EventArrivedEventArgs e)
            {
                try {
                    onServerClipboard();
                }
                catch (Exception ex) {
                    WmiBase.Singleton.DebugMsg("Server Clipboard Exception: "+ex.ToString());
                }
            }


            public void SetServerClipboard(string value)
            {
                if (currentclipboard != value)
                {
                    currentclipboard = value;
                    if (totalclientclipboard == null)
                    {
                        totalclientclipboard = value;
                        onClientClipboard();
                    }
                    else
                    {
                        currentclipboardchanged = true;
                    }
                }
            }
            void Finish()
            {
                clientwatch.Dispose();
                serverwatch.Dispose();
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
            ~ClipboardAccess()
            {
                Dispose(false);
            }

        }

        interface IDeprivClient
        {
            void SetClipboard(string value);
        }

        interface IWorkerProcessHandler
        {
            void WorkerProcessFinished();
        }

        class WorkerProcess : ICommServerImpl, IDeprivClient
        {
            CommServer comms;
            ClipboardAccess clipboard;
            WmiSession wmisession;
            IExceptionHandler exceptionhandler;
            IWorkerProcessHandler wphandler;
            object workerlock; // Locks changes in the state of the clipboard state machine's depriv client:

            bool workerconnected = false; // The depriv client has successfully connected to the service
            bool workerrunning = false; // A depriv client is currently running (but may not be connected)

            SafeWaitHandle worker;
            ProcessWaitHandle workerWaiter;
            RegisteredWaitHandle registeredWorkerWaiter;

            static public void AddToXDIgnoreApplicationList() {
                // XenDesktop in 'seamless' mode waits until all console process terminate before
                // ending a session.  Since XenDpriv runs forever, XD never ends a seamless session
                //
                // the solution is to ensure we are added to a registry entry at 
                // HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Citrix\wfshell\TWI\LogoffCheckSysModules 

                RegistryKey key = Registry.LocalMachine.CreateSubKey("SYSTEM\\CurrentControlSet\\Control\\Citrix\\wfshell\\TWI",RegistryKeyPermissionCheck.ReadWriteSubTree);
                string value = (string) key.GetValue("LogoffCheckSysModules","");
                if (string.IsNullOrEmpty(value)) {
                    value = Branding.Instance.getString("FILENAME_dpriv");
                }
                else {
                    if (!value.Contains(Branding.Instance.getString("FILENAME_dpriv")))
                    {
                        value = value + "," + Branding.Instance.getString("FILENAME_dpriv");
                    }
                }
                key.SetValue("LogoffCheckSysModules", value);

            }

            public WorkerProcess(ClipboardAccess clipboard, WmiSession wmisession, IExceptionHandler exceptionhandler, IWorkerProcessHandler wphandler, IntPtr consoletoken)
            {
                this.clipboard = clipboard;
                this.wmisession = wmisession;
                this.exceptionhandler = exceptionhandler;
                this.wphandler = wphandler;

                workerlock = new object();
                try
                {
                    comms = new CommServer(this);
                }
                catch (Exception e)
                {
                    wmisession.Log("Comms server failed to start:" + e.ToString());
                    throw;
                }
                try
                {
                    AddToXDIgnoreApplicationList();
                    string path = (string)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Citrix\\XenTools", "Install_Dir", "");
                    string fullpath = string.Format("{0}\\" + Branding.Instance.getString("FILENAME_dpriv"), path);
                    string cmdline = string.Format(Branding.Instance.getString("FILENAME_dpriv")+" {0}", comms.secret);
                    this.worker = new SafeWaitHandle(Win32Impl.CreateUserProcess(consoletoken, fullpath, cmdline), true);
                    workerWaiter = new ProcessWaitHandle(this.worker);
                    registeredWorkerWaiter = ThreadPool.RegisterWaitForSingleObject(workerWaiter, handleWorker, null, Timeout.Infinite, true);
                    this.workerrunning = true;
                    wmisession.Log("Worker Process spawned");
                }
                catch(Exception e)
                {
                    wmisession.Log("Worker process spawn exception : " + e.ToString());
                    comms.CloseMessagePipes();

                    throw;
                }
            }
            void handleWorker(object nothing, bool timeout)
            {
                wmisession.Log("Worker process has terminated");
                Stop(true);
            }
            public void Stop(bool callbackOnFinished)
            {
                lock (workerlock)
                {
                    if (workerrunning)
                    {
                        if (workerconnected)
                        {
                            clipboard.UnregisterClient();
                            workerconnected = false;
                        }
                        comms.CloseMessagePipes();

                        wmisession.Log("Stopping worker process " + worker.DangerousGetHandle().ToString());
                        registeredWorkerWaiter.Unregister(null);
                        try
                        {
                            // Don't kill the process.  If we have closed the Pipes, then that should be sufficient
                            // Win32Impl.KillProcess(worker.DangerousGetHandle(), 1);
                        }
                        catch
                        {
                            //If we fail to kill, we want to ignore this fact.  An error is already logged.
                        }
                        workerrunning = false;
                        if (callbackOnFinished) {
                            wphandler.WorkerProcessFinished();
                        }
                    }
                }
 
            }
            void IDeprivClient.SetClipboard(string value)
            {
                lock (workerlock)
                {
                    if (workerconnected)
                    {
                        comms.SendMessage(Communicator.SET_CLIPBOARD, value);
                    }
                }
            }
            void ICommunicator.HandleSetClipboard(string newvalue)
            {
                clipboard.SetServerClipboard(newvalue);
            }

            void ICommunicator.HandleFailure(string reason)
            {
                try {
                    wmisession.Log("Communications broken, resetting client : "+reason);
                    WmiBase.Singleton.DebugMsg("Comms failure :" + reason + "\n" + (new System.Diagnostics.StackTrace()).ToString());
                }
                catch {
                    // If our logging causes us to throw exceptions, ignore
                }
                finally {
                    Stop(true);
                }
            }
            void ICommunicator.HandleConnected(Communicator client)
            {
                wmisession.Log("Worker client connected");
                lock (workerlock)
                {
                    clipboard.RegisterClient(this);
                    this.workerconnected = true;
                }
                clipboard.PushClientClipboard();

            }

        }


        class ClipboardStateMachine : IWorkerProcessHandler
        {
            ClipboardAccess clipboard;
            WmiSession wmisession;
            
            bool running = true; // The clipboard thread is running and not intending to quit
            bool gotConsole = false; // We have access to a console on which to run the depriv client
            uint session;

            DateTime lastStartAttempt;
            int restartTime;

            WorkerProcess workerProcess = null;

            ManualResetEvent shutdownEvent = new ManualResetEvent(false);
            IExceptionHandler exceptionhandler;

            public ClipboardStateMachine(ClipboardAccess clipboard, WmiSession wmisession, IExceptionHandler exceptionhandler)
            {
                this.clipboard = clipboard;
                this.wmisession = wmisession;
                this.exceptionhandler = exceptionhandler;
                restartTime = 1000;
                lastStartAttempt = DateTime.UtcNow;
            }

            public void WorkerProcessFinished()
            {
                try
                {
                    workerProcess = null;
                    WmiBase.Singleton.DebugMsg("Worker process died");
                    if ((lastStartAttempt - DateTime.UtcNow) < 
                        TimeSpan.FromMilliseconds(restartTime)) {
                        Thread.Sleep(restartTime);
                        restartTime = restartTime * 2;
                        if (restartTime > 120000) {
                            restartTime = 120000;
                        }
                    }
                    else {
                        restartTime = 1000;
                    }
                    if (running)
                    {
                        wmisession.Log("Worker process restarting");
                        getConsoleAndSpawn();
                    }
                }
                catch (Exception e)
                {
                    exceptionhandler.HandleException("Clipboard handle worker", e);
                }
            }


            void restartWorker()
            {
                wmisession.Log("Restart worker");
                workerProcess.Stop(false);
                workerProcess = null;

                if (running)
                {
                    getConsoleAndSpawn();
                }
            }

            void getConsoleAndSpawn()
            {
                if (running)
                {
                    try {
                        session = Win32Impl.WTSGetActiveConsoleSessionId();
                        wmisession.Log("New session "+session.ToString());
                        if (session != 0xFFFFFFFF)
                        {
                            wmisession.Log("Checking to see if XenDesktop is active");
                            if (XenAppXenDesktop.ActiveConsoleSession(session))
                            {
                                wmisession.Log("Active XenDesktop session, not spawning worker");
                                gotConsole = false;
                                return;
                            }
                            Win32Impl.AcquireSystemPrivilege(Win32Impl.SE_TCB_NAME);
                            IntPtr consoletoken = IntPtr.Zero;
                            try
                            {
                                consoletoken = Win32Impl.QueryUserToken(session);
                                wmisession.Log("Got new session token");
                                gotConsole = true;
                                spawnWorker(consoletoken);
                            }
                            finally
                            {
                                Win32Impl.Close(consoletoken);
                            }
                        }
                    }
                    catch(Exception e) {
                        gotConsole = false;
                        WmiBase.Singleton.DebugMsg(e.ToString());
                    }
                }
                else
                {
                    wmisession.Log("Not got console");
                    gotConsole = false;
                }
            }


            void handleConsoleChanged()
            {
                if (running)
                {
                    wmisession.Log("Console changed");
                    if (gotConsole == false)
                    {
                        getConsoleAndSpawn();
                    }
                    else
                    {
                        restartWorker();
                    }
                }
            }


            void handleShutdown()
            {
                running = false;
                if (workerProcess != null)
                {
                    workerProcess.Stop(false);
                    workerProcess = null;
                }
            }


            public void Shutdown()
            {
                handleShutdown();
                shutdownEvent.Set();
            }


            void spawnWorker(IntPtr consoletoken)
            {
                if (running)
                {

                    wmisession.Log("Spawn Worker Process");
                    
                    if (workerProcess == null) {
                        try
                        {
                            lastStartAttempt = DateTime.UtcNow;
                            workerProcess = new WorkerProcess(clipboard, wmisession, exceptionhandler, this, consoletoken);
                        }
                        catch (Exception e)
                        {
                            wmisession.Log("Worker process failed to start " + e.ToString());
                            gotConsole = false;
                        }
                    }
                    else {
                        wmisession.Log("Unexpectedly trying to spawn a worker process while one is running");
                        gotConsole = false;
                    }

                }

            }

            public WaitHandle Run()
            {
                getConsoleAndSpawn();
                return this.shutdownEvent;
            }

            public void HandleConsoleChanged(System.ServiceProcess.SessionChangeReason reason)
            {
                try
                {
                    if ((reason == System.ServiceProcess.SessionChangeReason.ConsoleConnect) || 
                        (reason == System.ServiceProcess.SessionChangeReason.SessionLogon))
                    {
                        handleConsoleChanged();
                    }
                }
                catch (Exception ex)
                {
                    exceptionhandler.HandleException("Clipboard Changed", ex);
                }
            }
        }

        ClipboardStateMachine state;
        volatile bool running = false;
        private object statelock = new object();

        void shutdown()
        {
            running = false;
            state.Shutdown();
            clipboard.Dispose();
            wmisession.Dispose();
            done.Set();
        }

        void shutdownCallback(object nothing, bool timeout)
        {
            lock (statelock)
            {
                if (running)
                {
                    shutdown();
                }
            }
        }

        RegisteredWaitHandle shutdowncallback;
        ClipboardAccess clipboard;
        WmiSession wmisession;
        ManualResetEvent done = new ManualResetEvent(false);
        IExceptionHandler exceptionhandler;

        public WaitHandle Run()
        {

            WmiBase.Singleton.DebugMsg("Clipboard thread starting");
            wmisession = WmiBase.Singleton.GetXenStoreSession("Clipboard");
            clipboard = new ClipboardAccess(wmisession);

            state = new ClipboardStateMachine(clipboard, wmisession, exceptionhandler);
            lock (statelock)
            {
                running = true;
                WaitHandle threadhandle = state.Run();
                shutdowncallback = ThreadPool.RegisterWaitForSingleObject(threadhandle, shutdownCallback, null, Timeout.Infinite, true);
            }
            return done;

        }

        public void HandleSessionChange(System.ServiceProcess.SessionChangeReason changeargs, uint sessionId) {
            lock (statelock) {
                if (running && 
                    (sessionId==Win32Impl.WTSGetActiveConsoleSessionId())) {
                    state.HandleConsoleChanged(changeargs);
                }
            }
        }

        protected virtual void Finish()
        {
            lock (statelock)
            {
                if (running)
                {
                    shutdowncallback.Unregister(null);
                    shutdown();
                }
            }
            WmiBase.Singleton.DebugMsg("Clipboard thread finishing");
            
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
        ~ClipboardManager()
        {
            Dispose(false);
        }


    }


}
