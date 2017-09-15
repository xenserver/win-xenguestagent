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
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using XenGuestLib;


namespace xenwinsvc
{
    public class FeatureSnapshot : Feature, IRefresh
    {
        class CheckSupported
        {
            bool result = false;
            ManualResetEvent done;
            void checkSupportedThread() {
                Debug.Print("check snapshot");
                if (((Environment.OSVersion.Version.Major == 5) &&
                     (Environment.OSVersion.Version.Minor == 2) &&
                     ((new Win32Impl.WinVersion()).GetServicePackMajor() >= 1)) ||
                    ((Environment.OSVersion.Version.Major == 6) &&
                     ((Win32Impl.WinVersion.ProductType)(new Win32Impl.WinVersion()).GetProductType() != Win32Impl.WinVersion.ProductType.NT_WORKSTATION)))
                {
                    try
                    {
                        Debug.Print("create");
                        using (new VssSnapshot())
                        {
                            Debug.Print("success");
                            result = true;
                        }
                    }
                    catch(Exception e)
                    {
                        Debug.Print("exception");
                        Debug.Print(e.ToString());
                        result = false;
                    }
                    finally {
                        Debug.Print("done");
                        done.Set();
                    }
                }
                else {
                    result = false;
                    done.Set();
                }

            }
            public bool isSnapshotSupported()
            {
                done = new ManualResetEvent(false);
                Thread check = new Thread(checkSupportedThread, 256*1024);
                check.SetApartmentState(ApartmentState.STA);
                check.Start();
                done.WaitOne();
                return result;
            }

        }
        public static bool IsSnapshotSupported()
        {
            CheckSupported checker = new CheckSupported();
            return checker.isSnapshotSupported();
        }
        public static void removeSnapshot(WmiSession wmisession) {
            try
            {
                wmisession.GetXenStoreItem("control/snapshot").Remove();
            }
            catch { };
            try
            {
                wmisession.GetXenStoreItem("control/feature-snapshot").Remove();
            }
            catch { }
        }

        static volatile Thread snapshotThread = null;
        static object threadlock = new object();
        static bool stopping = false;
        public static void JoinSnapshots() 
        {
            lock (threadlock)
            {
                stopping = true;
                if (snapshotThread != null)
                {
                    snapshotThread.Join();
                    snapshotThread = null;
                }
            }
        }

        private static void startSnapshotThread(ThreadStart thread)
        {
            lock (threadlock)
            {
                if (stopping)
                {
                    WmiBase.Singleton.DebugMsg("Not starting new snapshot thread, feature is stopping");
                    return;
                }
                if (snapshotThread != null)
                {
                    snapshotThread.Join();
                }
                try
                {
                    snapshotThread = new Thread(thread, 256*1024);
                    snapshotThread.SetApartmentState(ApartmentState.STA);
                    snapshotThread.Start();
                }
                catch
                {
                    WmiBase.Singleton.SetError("CreateThread Snapshot");
                }
            }
        }

        public FeatureSnapshot(IExceptionHandler exceptionhandler)
            : base("snapshot", "", "control/snapshot/action",true, exceptionhandler)
        {
            actionKey = wmisession.GetXenStoreItem("control/snapshot/action");
            typeKey = wmisession.GetXenStoreItem("control/snapshot/type");
            statusKey = wmisession.GetXenStoreItem("control/snapshot/status");
            featureKey = wmisession.GetXenStoreItem("control/feature-snapshot");
            threadlock = new object();
        }

        XenStoreItem actionKey;
        XenStoreItem typeKey;
        XenStoreItem statusKey;
        XenStoreItem featureKey;

        private delegate StringBuilder CharallocCallback(int size);
        
        [DllImport("vssclient.dll")]
        private extern static void VssGetErrorMessage(IntPtr client, CharallocCallback callback);

        [DllImport("vssclient.dll")]
        private extern static IntPtr VssGetErrorCode(IntPtr client);
        
        [DllImport("vssclient.dll")]
        private extern static IntPtr VssGetErrorState(IntPtr client);

        class VssSnapshotException : Exception
        {
            private StringBuilder errormessage;
            public String message;
            public long code;
            public String state;
            private StringBuilder allocMessage(int size)
            {
                errormessage = new StringBuilder(size);
                return errormessage;
            }
            public VssSnapshotException(IntPtr client) : base()
            {
                VssGetErrorMessage(client, allocMessage);
                code = (long)VssGetErrorCode(client);
                state = VssGetErrorState(client).ToString();
                message = errormessage.ToString() + " code " + code.ToString();
            }
            public VssSnapshotException()
                : base()
            {
                message = "VSS Support Not found";
            }
            override public string ToString()
            {
                return message + "\n" + base.ToString();
            }
        }
        class VssSnapshot : IDisposable {

            public enum Type : uint
            {
                VM = 0,
                VOLUME = 1
            };

            [DllImport("vssclient.dll")]
            private extern static IntPtr VssClientInit(VssSnapshot.Type type);

            [DllImport("vssclient.dll", CharSet = CharSet.Unicode)]
            private extern static void VssClientAddVolume(IntPtr handle, string toadd ); 

            IntPtr client;

            WmiSession wmisession;

            public VssSnapshot() : this(VssSnapshot.Type.VM, new List<string>()){}
            VssSnapshot.Type type;
            List<string> volumes;
            public VssSnapshot(VssSnapshot.Type type, List<string> volumes) {
                if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
                {
                    throw new Exception("VssSnapshot must be initialised and used in a Single Thread Apartment");
                }
                this.type = type;
                this.volumes = volumes;
                wmisession = WmiBase.Singleton.GetXenStoreSession("Snapshot");
                client = VssClientInit(type);
                if (client.Equals(IntPtr.Zero))
                {
                    throw new VssSnapshotException();
                }
                foreach (string vol in volumes)
                {
                    Debug.Print("Attempting to add" + vol);
                    VssClientAddVolume(client, vol);
                }
            }

            bool backupHandler(IntPtr bstrMem)
            {
                try
                {
                    string backup = Marshal.PtrToStringBSTR(bstrMem);
                    string vm = wmisession.GetXenStoreItemCached("vm").value.Substring(4);
                    if (type == VssSnapshot.Type.VM)
                    {
                        string snapstr = wmisession.GetXenStoreItem("/vss/" + vm + "/snapuuid").value;
                        wmisession.GetXenStoreItem("control/snapshot/snapuuid").value = snapstr;
                        wmisession.GetXenStoreItem("control/snapshot/type").value = "vm";
                    }
                    else if (type == VssSnapshot.Type.VOLUME)
                    {
                        string rootkeyname = "/vss/" + vm + "/vdisnap";
                        XenStoreItem vdisnap = wmisession.GetXenStoreItem(rootkeyname);
                        foreach (string entryKey in vdisnap.children)
                        {
                            XenStoreItem src = wmisession.GetXenStoreItem(entryKey);
                            XenStoreItem dest = wmisession.GetXenStoreItem("control/snapshot/vdi/" + entryKey.Substring(rootkeyname.Length + 1));
                            src.value = dest.value;
                        }
                        string snaptype = wmisession.GetXenStoreItem("/vss/" + vm + "/snaptype").value;
                        wmisession.GetXenStoreItem("control/snapshot/type").value = snaptype;
                    }

                    wmisession.GetXenStoreItem("control/snapshot/snapid").Remove();

                    int size = backup.Length;
                    int poscount = 0;
                    int pagecount = 0;
                    string substr = "";
                    while (size > 0)
                    {
                        substr += string.Format("{0:x2}{1:x2}", ((int)backup[poscount])&0xff, (((int)backup[poscount])>>8)&0xff);
                        size--;
                        poscount++;
                        if (((poscount % 255) == 0) || (size == 0))
                        {
                            wmisession.GetXenStoreItem("control/snapshot/snapid/" + pagecount.ToString()).value = substr;
                            substr = "";
                            pagecount++;
                        }
                    }
                }
                catch(Exception e)
                {
                    try {
                        wmisession.GetXenStoreItem("control/snapshot/snapid").Remove();
                        WmiBase.Singleton.DebugMsg("Backup failed: " + e.ToString());
                    }
                    catch {}
                    return false;
                }
                return true;


            }
            private delegate bool SaveBackupDocCallback(IntPtr bstr);
            [DllImport("vssclient.dll")]
            [return: MarshalAs(UnmanagedType.I1)]
            private extern static bool VssClientCreateSnapshotSet(IntPtr handle, SaveBackupDocCallback callback);
            public void CreateSnapshotSet() {
                if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
                {
                    throw new Exception("VssSnapshot must be used in a Single Thread Apartment");
                }
                if (!VssClientCreateSnapshotSet(client, backupHandler))
                {
                     WmiBase.Singleton.DebugMsg("Unable to create snapshot set");
                    throw new VssSnapshotException(client);
                }
                WmiBase.Singleton.DebugMsg("Created snapshot set");
            }


            [DllImport("vssclient.dll")]
            private extern static void VssClientDestroy(IntPtr handle);
            protected virtual void Finish()
            {
                if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
                {
                    throw new Exception("VssSnapshot must be destroyed in a Single Thread Apartment");
                }
                VssClientDestroy(client);
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
            ~VssSnapshot()
            {
                Dispose(false);
            }
        }

        VssSnapshot.Type type;

        List<string> ListXenVolumes()
        {
            List<string> list = new List<string>();
            Win32Impl.Volumes volumes= new Win32Impl.Volumes();
            foreach (Win32Impl.Volume volume in volumes)
            {
                list.Add(volume.Name);
            }
            return list;
        }

        List<string> ListXenStoreVolumes()
        {
            List<string> list = new List<string>();
            string[] mountpoints = wmisession.GetXenStoreItem("control/snapshot/volume").children;
            foreach (string mountpoint in mountpoints) {
                string name = wmisession.GetXenStoreItem(mountpoint).value;
                try {
                    list.Add(Win32Impl.GetVolumeNameFromMountPoint(name));
                }
                catch {};
            }
            return list;
        }

        private enum HRESULT : long
        {
            S_OK = 0x00000000,
            S_FALSE = 0x00000001,
            // VSS Info/Success
            VSS_S_ASYNC_PENDING = 0x42309,
            VSS_S_ASYNC_FINISHED = 0x4230A,
            VSS_S_ASYNC_CANCELLED = 0x4230B,
            VSS_S_SOME_SNAPSHOTS_NOT_IMPORTED = 0x42320,
            // VSS Errors
            VSS_E_BAD_STATE = 0x80042301,
            VSS_E_PROVIDER_ALREADY_REGISTERED = 0x80042303,
            VSS_E_PROVIDER_NOT_REGISTERED = 0x80042304,
            VSS_E_PROVIDER_VETO = 0x80042306,
            VSS_E_PROVIDER_IN_USE = 0x80042307,
            VSS_E_OBJECT_NOT_FOUND = 0x80042308,
            VSS_E_VOLUME_NOT_SUPPORTED = 0x8004230C,
            VSS_E_VOLUME_NOT_SUPPORTED_BY_PROVIDER = 0x8004230E,
            VSS_E_OBJECT_ALREADY_EXISTS = 0x8004230D,
            VSS_E_UNEXPECTED_PROVIDER_ERROR = 0x8004230F,
            VSS_E_CORRUPT_XML_DOCUMENT = 0x80042310,
            VSS_E_INVALID_XML_DOCUMENT = 0x80042311,
            VSS_E_MAXIMUM_NUMBER_OF_VOLUMES_REACHED = 0x80042312,
            VSS_E_FLUSH_WRITES_TIMEOUT = 0x80042313,
            VSS_E_HOLD_WRITES_TIMEOUT = 0x80042314,
            VSS_E_UNEXPECTED_WRITER_ERROR = 0x80042315,
            VSS_E_SNAPSHOT_SET_IN_PROGRESS = 0x80042316,
            VSS_E_MAXIMUM_NUMBER_OF_SNAPSHOTS_REACHED = 0x80042317,
            VSS_E_WRITER_INFRASTRUCTURE = 0x80042318,
            VSS_E_WRITER_NOT_RESPONDING = 0x80042319,
            VSS_E_WRITER_ALREADY_SUBSCRIBED = 0x8004231A,
            VSS_E_UNSUPPORTED_CONTEXT = 0x8004231B,
            VSS_E_VOLUME_IN_USE = 0x8004231D,
            VSS_E_MAXIMUM_DIFFAREA_ASSOCIATIONS_REACHED = 0x8004231E,
            VSS_E_INSUFFICIENT_STORAGE = 0x8004231F,
            VSS_E_NO_SNAPSHOTS_IMPORTED = 0x80042320
        }

        private string GetHresult(long hresult)
        {
            if (!Enum.IsDefined(typeof(HRESULT), hresult))
                return "Unknown";
            try { return ((HRESULT)hresult).ToString("G"); }
            catch { return "Unknown"; }
        }

        void snapshotThreadHandler()
        {
            try
            {
                WmiBase.Singleton.DebugMsg("SnapshotThread");


                if (!typeKey.Exists())
                {
                    type = VssSnapshot.Type.VM;
                }
                else
                {
                    switch (typeKey.value)
                    {
                        case "volume":
                            type = VssSnapshot.Type.VOLUME;
                            break;
                        case "vm":
                        default:
                            type = VssSnapshot.Type.VM;
                            break;
                    }
                }

                List<String> volumeNames;

                if (type == VssSnapshot.Type.VM)
                {
                    volumeNames = ListXenVolumes();
                }
                else
                {
                    volumeNames = ListXenStoreVolumes();
                }

                using (VssSnapshot vss = new VssSnapshot(type, volumeNames))
                {

                    Debug.Print("Create snapshot");

                    vss.CreateSnapshotSet();

                    Debug.Print("Created snapshot");
                    statusKey.value = "snapshot-created";
                }
            }
            catch (VssSnapshotException vsse)
            {
                Debug.Print(vsse.ToString());
                try {
                    wmisession.GetXenStoreItem("control/snapshot/error/message").value = GetHresult(vsse.code);
                    wmisession.GetXenStoreItem("control/snapshot/error/code").value = vsse.code.ToString();
                    wmisession.GetXenStoreItem("control/snapshot/error").value = vsse.state;
                    statusKey.value = "snapshot-error";
                }
                catch{}
            }
            catch (Exception e) 
            {
                Debug.Print(e.ToString());
                try {
                    wmisession.GetXenStoreItem("control/snapshot/error").value = "Unknown Error";
                    statusKey.value = "snapshot-error";
                }
                catch {}
            }

            Debug.Print("Snapshot done");
        }

        override protected void onFeature()
        {
            Debug.Print("Snapshot on feature");
            if ((!controlKey.Exists()) ||
                (controlKey.value != "create-snapshot"))
                return;

            Debug.Print("Manage keys");
            controlKey.Remove();
            actionKey.Remove();
            Debug.Print("Call background thread");
            startSnapshotThread(snapshotThreadHandler);
        }

        volatile bool licensed = false;
        bool xenwinsvc.IRefresh.NeedsRefresh()
        {
            if (licensed != FeatureVSSLicensed.IsLicensed())
                return true;
            
            return false;
        }
        bool xenwinsvc.IRefresh.Refresh(bool force)
        {
            licensed = FeatureVSSLicensed.IsLicensed();
            featureKey.value = licensed ? "1" : "0";
            return true;
        }
    }
}
