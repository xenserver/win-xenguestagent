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
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using System.Management;
using System.Windows.Forms;

namespace xenwinsvc
{
    public sealed class WmiBase
    {
        private static volatile WmiBase instance;
        private static object syncRoot = new Object();
        private WmiSession wmisession;
        private object syncSingleton;
        private XenStoreItem xsupdated;
        private XenStoreItem xsupdatedcount;
        private XenStoreItem xserror;
        private int updatecounter = 0;

        private Queue<string> debugmsg;







        public static bool HandleManagementException(ManagementException manex) {
            if ((manex.ErrorCode == ManagementStatus.InvalidObject) ||
                (manex.ErrorCode == ManagementStatus.InvalidClass))
            {
                return true;
            }
            return false;
        }

        public ManagementEventWatcher GetListenerEvent(string eventname)
        {
            if (eventname == "__InstanceModificationEvent")
            {
                WqlEventQuery eq = new WqlEventQuery(eventname, new TimeSpan(0, 0, 1), "TargetInstance ISA \"Win32_TerminalServiceSetting\"");
                return new ManagementEventWatcher(Win32_TerminalServiceSetting.Scope, eq);
            }
            else
            {
                WqlEventQuery eq = new WqlEventQuery(eventname);
                return new ManagementEventWatcher(Scope, eq);
            }
        }

        private ManagementObject getBase()
        {

                ManagementPath mpath = new ManagementPath("CitrixXenStoreBase");
                ManagementClass manclass = new ManagementClass(scope, mpath, null);
                ManagementObjectCollection moc = manclass.GetInstances();
                return getFirst(moc);

        }

        public WmiListener ListenForEvent(string eventname, EventArrivedEventHandler handler)
        {
            ManagementEventWatcher ev = GetListenerEvent(eventname);
            ev.EventArrived += handler;
            return new WmiListener(ev);
        }

        public ManagementObject Win32_TerminalServiceSetting
        {
            get
            {
                ManagementObject win32TerminalServiceSetting = null;
                string root = "root\\cimv2\\terminalservices";
                if (Environment.OSVersion.Version.Major < 6)
                {
                    root = "root\\cimv2";
                }
                ManagementClass mc = new ManagementClass(root, "Win32_TerminalServiceSetting", null);
                mc.Scope.Options.EnablePrivileges = true;
                win32TerminalServiceSetting = WmiBase.getFirst(mc.GetInstances());

                return win32TerminalServiceSetting;
            }
        }

        private ManagementObject win32OperatingSystem = null;
        public ManagementObject Win32_OperatingSystem
        {
            get
            {
                ManagementClass mc = new ManagementClass("Win32_OperatingSystem");
                mc.Scope.Options.EnablePrivileges = true;
                win32OperatingSystem = WmiBase.getFirst(mc.GetInstances());
                win32OperatingSystem.Scope.Options.EnablePrivileges = true;
                return win32OperatingSystem;
            }
        }

        private ManagementObject win32ComputerSystem = null;
        public ManagementObject Win32_ComputerSystem
        {
            get
            {
                ManagementClass mc = new ManagementClass("Win32_ComputerSystem");
                mc.Scope.Options.EnablePrivileges = true;
                win32ComputerSystem = WmiBase.getFirst(mc.GetInstances());
                return win32ComputerSystem;
            }
        }

        private ManagementObjectCollection win32QuickFixEngineering = null;
        public ManagementObjectCollection Win32_QuickFixEngineering
        {
            get
            {
                ManagementClass mc = new ManagementClass("Win32_QuickFixEngineering");
                mc.Scope.Options.EnablePrivileges = true;
                win32QuickFixEngineering = mc.GetInstances();
                return win32QuickFixEngineering;
            }
        }

        private ManagementObjectCollection win32NetworkAdapterConfiguration = null;
        public ManagementObjectCollection Win32_NetworkAdapterConfiguration
        {
            get
            {
                 ManagementClass mc = new ManagementClass("Win32_NetworkAdapterConfiguration");
                 mc.Scope.Options.EnablePrivileges = true;
                 win32NetworkAdapterConfiguration = mc.GetInstances();
                 return win32NetworkAdapterConfiguration;
            }
        }

        const int DEBUG_SIZE = 200;
        public void DebugMsg(string message) {
            Debug.Print(message);
            try {
                lock (syncSingleton) {
                    if (debugmsg.Count == DEBUG_SIZE)
                    {
                        debugmsg.Dequeue();
                    }
                    debugmsg.Enqueue(message);
                }
            }
            catch(Exception e) {
                wmisession.Log("Message Logger Failure\n"+e.ToString());
            }
        }
        public void DumpDebugMsg() {
            while (debugmsg.Count > 0) {
                wmisession.Log("LOGGED: "+debugmsg.Dequeue());
            }
        }

        private WmiBase() 
        {
            debugmsg = new Queue<string>();
            syncSingleton = new Object();
            xenbaselock = new Object();
            wmisession = new WmiSession("Base", this);
            xsupdated = wmisession.GetXenStoreItem("data/updated");
            xsupdatedcount = wmisession.GetXenStoreItem("data/update_cnt");
            xserror = wmisession.GetXenStoreItem("control/error");
        }

        public void SetError(string function)
        {
            xserror.value = function + " failed.";
        }

        public ulong XenTime
        {
            get
            {
                return (ulong)(getBase()["XenTime"]);
            }
        }
        
        bool keymayhavechanged = false;
        int changecount; // a value which is incremented every time a 
                         // change is made to xenstore by us

        public int GetChangeCount()
        {
            lock (syncSingleton)
            {
                return changecount;
            }
        }
        public void XenStoreChanged()
        {
            lock (syncSingleton)
            {
                changecount++;
                keymayhavechanged = true;
            }
        }
        
        public void Kick(bool force=true) {
            lock (syncSingleton) 
            {
                // We kick eiter if we are told to (the default), or
                // if we know we have changed xenstore since we last
                // kicked
                if (force || keymayhavechanged) {
                    updatecounter++;
                    xsupdated.value = "1";
                    xsupdatedcount.value = updatecounter.ToString();
                    keymayhavechanged = false;
                }
            }
        }

        public static ManagementObject getFirst(ManagementObjectCollection collection)
        {
            if (collection.Count < 1)
                throw new Exception("No objects found");
            foreach (ManagementObject mobj in collection)
                return mobj;
            throw new Exception("No objects found");
        }

        public WmiSession GetXenStoreSession(string name)
        {
            lock (syncSingleton)
            {

                WmiSession session = new WmiSession(name, this);

                return session;
            }
        }

        private ManagementScope scope =null;
        public ManagementScope Scope {
            get
            {
                lock (syncSingleton)
                {
                    if (scope == null)
                    {
                        scope = new ManagementScope("root\\wmi");
                        scope.Connect();
                    }
                    return scope;
                }
            }
        }

        private ManagementObject xenbase=null;

        static public void Reset()
        {
            lock (syncRoot)
            {
                instance = null;
            }
        }

        object xenbaselock;




        public ManagementObject XenBase
        {
            get
            {
                lock (xenbaselock)
                {
                    if (xenbase == null)
                    {
                        xenbase = getBase();
                        return xenbase;
                    }
                    else
                    {
                        return xenbase;
                    }
                }
            }
        }

        public static bool Check()
        {
            try
            {
                return Singleton.intCheck();
            }
            catch
            {
                return false;
            }
        }

        private bool intCheck()
        {
            try
            {
                getBase();
            }
            catch
            {
                return false;
            }
            return true;

        }





        public static WmiBase Singleton
        {
            get
            {
                if (instance == null)
                {
                    lock (syncRoot)
                    {
                        if (instance == null)
                        {

                            instance = new WmiBase();

                        }
                    }
                }

                return instance;
            }
        }
       
    }

    public class XenStoreItemCached : XenStoreItem
    {
        public XenStoreItemCached(string name, WmiSession wmisession) : base(name, wmisession) { }
        bool initialised = false;
        string contents;
        override public string ToString()
        {
            return "Path :" + this.name +
                 "\nCached :" + this.contents;
        }
        public override string value
        {
            get
            {
                try
                {
                    initialised = true;
                    try
                    {
                        contents = base.value;
                    }
                    catch
                    {
                        WmiBase.Singleton.DebugMsg("Unable to access base");
                        throw;
                    }
                    return contents;
                }
                catch
                {
                    WmiBase.Singleton.DebugMsg("Unable to read " + base.name);
                    throw;
                }
            }
            set
            {
                try
                {
                    string toset = value;
                    if (toset ==null )
                    {
                        toset = "";
                    }
                    if ((!initialised) || (!contents.Equals(toset)))
                    {
                        try
                        {
                            base.value = toset;
                            initialised = true;
                        }
                        catch
                        {
                            Debug.Print("Base is : " + base.ToString());
                            Debug.Print("Value is : " + toset);
                            if (toset.Equals(""))
                            {
                                Debug.Print("toset is the empty string");
                            }
                            if (toset.Equals(null))
                            {
                                Debug.Print("toset is null");
                            }
                            WmiBase.Singleton.DebugMsg("Unable to access base "+base.ToString());
                            throw;
                        }
                        contents = toset;
                    }
                }
                catch
                {
                    WmiBase.Singleton.DebugMsg("Unable to write " + base.name+" "+value);
                    throw;
                }
            }
        }

        public override void Remove()
        {
            initialised = false;
            base.Remove();
        }
    }

    public class XenStoreItem
    {
        WmiSession wmisession;
        protected string name;

        public string GetName() {
            return name;
        }

        public XenStoreItem(string name, WmiSession wmisession)
        {
            this.wmisession = wmisession;
            this.name = name;
        }

        public WmiWatchListener Watch(EventArrivedEventHandler handler)
        {
            WqlEventQuery eq = new WqlEventQuery("CitrixXenStoreWatchEvent", String.Format("EventId=\"{0}\"", name));
            ManagementEventWatcher ev = new ManagementEventWatcher(WmiBase.Singleton.Scope, eq);
            ev.EventArrived += handler;
            return new WmiWatchListener(wmisession, ev, name);
        }

        ManagementStatus status = ManagementStatus.NoError;
        public ManagementStatus GetStatus()
        {
            return status;
        }
        bool readfail = false;
        bool writefail = false;
        bool childfail=false;
        public virtual string value
        {
            get
            {
                try
                {
                    string res = wmisession.GetValue(name);
                    readfail = false;
                    status = ManagementStatus.NoError;
                    return res;
                }
                catch(ManagementException e) {
                    status = e.ErrorCode;
                    if (e.ErrorCode == ManagementStatus.AccessDenied) {
                        if (!readfail) {
                            wmisession.Log("Access Denied reading "+name);
                            readfail = true;
                        }
                    }
                    throw e;
                }
                catch
                {
                    status = ManagementStatus.Failed;
                    wmisession.Log("Get value failed: " + name);
                    throw;
                }
            }
            set
            {
                string toset = value;
                if (toset.Equals(null))
                {
                    toset = "";
                }
                try {
                    wmisession.SetValue(name, toset);
                    writefail = false;
                    status = ManagementStatus.NoError;
                }
                catch(ManagementException e) {
                    status = e.ErrorCode;
                    if (e.ErrorCode == ManagementStatus.AccessDenied) {
                        if (!writefail) {
                            wmisession.Log("Access Denied writing "+name);
                            writefail = true;
                        }
                    }
                    else {
                        throw e;
                    }
                }
                catch
                {
                    status = ManagementStatus.Failed;
                    wmisession.Log("Set value failed: " + name);
                    throw;
                }

            }
        }
        public string[] children
        {
            get
            {
                try
                {
                    string[] kids =  wmisession.GetChildren(name);
                    childfail = false;
                    status = ManagementStatus.NoError;
                    return kids;
                }
                catch(ManagementException me)
                {
                    status = me.ErrorCode;
                    if (me.ErrorCode == ManagementStatus.AccessDenied) {
                        if (!childfail) {
                            wmisession.Log("Access Denied fetching children "+name);
                            childfail = true;
                        }
                    }
                    else {
                        wmisession.Log("GetChildren failed: " + name + " " +me.ErrorCode.ToString());
                    }
                    throw;
                }
                catch(Exception e)
                {
                    status = ManagementStatus.Failed;
                    wmisession.Log("GetChildren failed: " + name+" "+e.ToString());
                    throw;
                }
            }
        }
        public virtual void Remove()
        {
            try
            {
                    wmisession.RemoveValue(name);
                    writefail = false;
                    status = ManagementStatus.NoError;
            }
            catch(ManagementException me)
            {
                status = me.ErrorCode;
                if (me.ErrorCode == ManagementStatus.AccessDenied) {
                    if (!writefail)
                    {   
                        wmisession.Log("Remove failed: " + name + " Access Denied");
                        writefail = true;
                    }
                }
                else {
                    wmisession.Log("Remove failed: " + name +" "+me.ErrorCode.ToString());
                    throw;
                }
            }
            catch {
                status = ManagementStatus.Failed;
                wmisession.Log("Remove failed: " + name);
                throw;
            }
        }
        public bool Exists()
        {
            try
            {
                wmisession.GetValue(name);
                return true;
            }
            catch(ManagementException me) {
                if (me.ErrorCode == ManagementStatus.AccessDenied) {
                    throw;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }



    public class WmiSession : IDisposable
    {
        private ManagementObject session = null;
        private WmiBase wmibase;
        private Dictionary<string, XenStoreItem> items;
        private Dictionary<string, XenStoreItemCached> cacheditems;

        public void StartTransaction()
        {
            session.InvokeMethod("StartTransaction", null);
        }
        public void CommitTransaction()
        {
            session.InvokeMethod("CommitTransaction", null);
        }
        public void AbortTransaction()
        {
            session.InvokeMethod("AbortTransaction", null);
        }

        public void AddWatch(string pathname) {

            ManagementBaseObject inparam = session.GetMethodParameters("SetWatch");
            inparam["PathName"] = pathname;
            ManagementBaseObject outparam = session.InvokeMethod("SetWatch", inparam, null);
        }

        public void RemoveWatch(string pathname)
        {
            ManagementBaseObject inparam = session.GetMethodParameters("RemoveWatch");
            inparam["PathName"] = pathname;
            ManagementBaseObject outparam = session.InvokeMethod("RemoveWatch", inparam, null);
        }

        string name;
        public WmiSession(string sessionqualifier, WmiBase wmibase)
        {
            items = new Dictionary<string, XenStoreItem>();
            cacheditems = new Dictionary<string, XenStoreItemCached>();
            this.wmibase = wmibase;
            name = sessionqualifier;

            // First check to see if a "Citrix Xen Service" session exists.  If
            // so, we use that
            // Otherwise we have to create a new session from scratch
            try
            {
                ObjectQuery obq = new ObjectQuery(String.Format("SELECT * from CitrixXenStoreSession WHERE Id=\"Citrix Xen Service: {0}\"", sessionqualifier));
                ManagementObjectSearcher mobs = new ManagementObjectSearcher(wmibase.Scope, obq); ;
                session = WmiBase.getFirst(mobs.Get());
                session.InvokeMethod("EndSession", null);
            }
            catch
            {
            }
            ManagementBaseObject inparam = wmibase.XenBase.GetMethodParameters("AddSession");
            inparam["ID"] = String.Format("Citrix Xen Service: {0}", sessionqualifier);
            ManagementBaseObject outparam = wmibase.XenBase.InvokeMethod("AddSession", inparam, null);
            UInt32 sessionid = (UInt32)outparam["SessionId"];
            ObjectQuery query = new ObjectQuery("SELECT * from CitrixXenStoreSession WHERE SessionId=" + sessionid.ToString());
            ManagementObjectSearcher objects = new ManagementObjectSearcher(wmibase.Scope, query); ;
            session = WmiBase.getFirst(objects.Get());
        }

        public XenStoreItem GetXenStoreItem(string name)
        {
            /*if (!items.ContainsKey(name))
            {
                items.Add(name, new XenStoreItem(name, this));
            }*/
            return new XenStoreItem(name, this); /*items[name]*/;
        }
        public XenStoreItemCached GetXenStoreItemCached(string name)
        {
            if (!cacheditems.ContainsKey(name))
            {
                cacheditems.Add(name, new XenStoreItemCached(name, this));
            }
            return cacheditems[name];
        }

        void Finish()
        {
            try {
                session.InvokeMethod("EndSession", null);
            }
            catch{}

        }

        public void Log(string message)
        {

            foreach (string line in (name + " : " + message).Split('\n'))
            {
                Debug.Print(line + "\n");
                try
                {
                    ManagementBaseObject inparam = session.GetMethodParameters("Log");
                    inparam["Message"] = line;
                    ManagementBaseObject outparam = session.InvokeMethod("Log", inparam, null);
                }
                catch { 
                }
            }
  
        }

        internal void RemoveValue(string pathname)
        {   
            ManagementBaseObject inparam = session.GetMethodParameters("RemoveValue");
            inparam["PathName"] = pathname;
            ManagementBaseObject outparam = session.InvokeMethod("RemoveValue", inparam, null);
            WmiBase.Singleton.XenStoreChanged();
        }


        private string getFirstChild(string pathname) {
            ManagementBaseObject inparam;
            ManagementBaseObject outparam;
            inparam = session.GetMethodParameters("GetFirstChild");
            inparam["InPath"] = pathname;
            outparam = session.InvokeMethod("GetFirstChild", inparam, null);
            if (outparam==null || outparam["OutPath"] == null) {
                return "";
            }
            return (string)outparam["OutPath"];
        }
        private string getNextSibling(string pathname) {
            ManagementBaseObject inparam;
            ManagementBaseObject outparam;
            inparam = session.GetMethodParameters("GetNextSibling");
            inparam["InPath"] = pathname;
            outparam = session.InvokeMethod("GetNextSibling", inparam, null);
            if (outparam==null || outparam["OutPath"] == null) {
                return "";
            }
            return (string)outparam["OutPath"];

        }

        private string[] getChildrenManually(string pathname) {
            List<string> outlist = new List<string>();
            string next = getFirstChild(pathname);
            while (! next.Equals("")) {
                outlist.Add(next);
                next = getNextSibling(next);
            }
            return outlist.ToArray();
        }

        internal string[] GetChildren(string pathname)
        {
            ManagementBaseObject inparam;
            ManagementBaseObject outparam;
            ManagementBaseObject cnode;

            inparam = session.GetMethodParameters("GetChildren");
            inparam["PathName"] = pathname;
            try {
            outparam = session.InvokeMethod("GetChildren", inparam, null);
            }
            catch {
                /*GetChildren fails on windows vists sp0, so we have a backup*/
                return getChildrenManually(pathname);
            }
            if (outparam==null)
                return new string[0];
            cnode = (ManagementBaseObject)outparam["children"];
            return (String[])cnode["ChildNodes"];

        }

        internal string GetValue(string pathname)
        {

            ManagementBaseObject inparam = session.GetMethodParameters("GetValue");
            inparam["PathName"] = pathname;

            ManagementBaseObject outparam = session.InvokeMethod("GetValue", inparam, null);

            return (String)outparam["value"];
        }
        internal void SetValue(string pathname, string value)
        {

            ManagementBaseObject inparam = session.GetMethodParameters("SetValue");
            inparam["PathName"] = pathname;
            inparam["value"] = value;
            ManagementBaseObject outparam = session.InvokeMethod("SetValue", inparam, null);
            WmiBase.Singleton.XenStoreChanged();
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
        ~WmiSession()
        {
            Dispose(false);
        }

    }
    public class WmiWatchListener : WmiListener
    {
        string pathname;
        WmiSession session;
        public WmiWatchListener(WmiSession session, ManagementEventWatcher ev, string pathname) : base( ev)
        {
            this.pathname = pathname;
            this.session = session;
            session.AddWatch(pathname);
        }
        override protected void Finish()
        {
            base.Finish();
            try {
                session.RemoveWatch(pathname);
            }
            catch {
                // Ignore, we've lost WMI connection
            }
        }
    }



    public class WmiListener : IDisposable
    {
        ManagementEventWatcher ev;

        public WmiListener(ManagementEventWatcher ev)
        {
            this.ev = ev;
        
            ev.Start();
            
        }
        protected virtual void Finish()
        {
            try {
                ev.Stop();
                ev.Dispose();
            }
            catch{}
            
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
        ~WmiListener()
        {
            Dispose(false);
        }
    }

}
