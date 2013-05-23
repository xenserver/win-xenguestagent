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

namespace xenwinsvc
{

    public interface IRefresh : IDisposable
    {
        bool NeedsRefresh();

        bool Refresh(bool force);
    }

    public class Refresher
    {
        static IExceptionHandler exceptionhandler;
        static object refreshlock = new object();
        static Stack<IRefresh> refreshers = new Stack<IRefresh>();
        static Timer timer = null;
        static public void Add(IRefresh refresher)
        {
            lock (refreshlock)
            {
                refreshers.Push(refresher);
                Disposer.Add(refresher);
            }
        }
        static public void Dispose()
        {
            lock (refreshlock)
            {
                if (timer != null) {
                    timer.Dispose();
                }
                while (refreshers.Count > 0)
                {
                    refreshers.Pop();
                }
            }
        }
        static public void RefreshAll(bool force)
        {
            try {
                lock (refreshlock) {
                    foreach (IRefresh torefresh in refreshers)
                    {
                       torefresh.Refresh(force);
                    }
                }
                WmiBase.Singleton.Kick(force:true);
            }
            catch (System.Management.ManagementException e) {
                if (e.ErrorCode != System.Management.ManagementStatus.AccessDenied) {
                    throw;
                }
            }
        }

        static private void onTimer(object nothing) {
            try {
                bool needskick = false;
                lock (refreshlock) {
                    foreach (IRefresh torefresh in refreshers)
                    {
                        if (torefresh.NeedsRefresh())
                        {
                            // We only want to kick a xenstore refresh if the
                            // refresher tells us we should
                            needskick |= torefresh.Refresh(false);
                        }
                    }
                    if (needskick)
                    {
                        // We only want to kick a refresh if anything in xenstore
                        // has changed
                        WmiBase.Singleton.Kick(force:false);
                    }
                }
            }
            catch (System.Management.ManagementException e) {
                if (e.ErrorCode != System.Management.ManagementStatus.AccessDenied) {
                    exceptionhandler.HandleException("Refreshing",e);
                }
            }
            catch (Exception ex){
                exceptionhandler.HandleException("Refreshing",ex);
            }
        }


        static public void Run(IExceptionHandler exhandler)
        {
            exceptionhandler=exhandler;
            timer = new Timer(onTimer, null, 4500, 4500);
        }

        

    }

    public class VolumeInfo : IRefresh
    {
        int refreshCount = 0;
        public bool NeedsRefresh()
        {
            refreshCount++;
            if (refreshCount > 26)
            {
                return true;
            }
            return false;
        }
        class StoredVolumes
        {
            static long nextid = 0;

            Dictionary<string, StoredVolume> storedVolumes;
            WmiSession wmisession;
            public StoredVolumes(WmiSession wmisession)
            {
                storedVolumes = new Dictionary<string, StoredVolume>();
                nextid = 0;
                this.wmisession = wmisession;
            }

            public bool update(Win32Impl.Volumes volumes)
            {
                int changecount = WmiBase.Singleton.GetChangeCount();
                Dictionary<string, StoredVolume> newVolumes = new Dictionary<string, StoredVolume>();
                foreach (Win32Impl.Volume volume in volumes)
                {
                    if (storedVolumes.ContainsKey(volume.Name))
                    {
                        newVolumes[volume.Name] = storedVolumes[volume.Name].Change(volume);
                        storedVolumes.Remove(volume.Name);
                    }
                    else
                    {
                        newVolumes[volume.Name] = new StoredVolume(volume, wmisession, nextid);
                        nextid++;
                    }
                }

                foreach (StoredVolume svol in storedVolumes.Values)
                {
                    svol.Remove();
                }
                storedVolumes = newVolumes;
                int newchangecount = WmiBase.Singleton.GetChangeCount() ;
                if( newchangecount!= changecount) {
                    // This indicates that we may well have changed siomething in xenstore
                    return true;
                }
                // This indicates nothing in xenstore has changed
                return false;
            }

            class StoredVolume
            {
                long Id;
                               
                XenStoreItemCached name;
                XenStoreItemCached size;
                XenStoreItemCached free;
                XenStoreItemCached volumeName;
                XenStoreItemCached filesystem;

                List<XenStoreItemCached> mountPoints;
                List<XenStoreItemCached> extents;

                ulong freebytes;

                string getVolumePath()
                {
                    return String.Format("data/volumes/{0}", Id);
                }

                string findDiskName(uint diskNumber)
                {
                    foreach (string device in wmisession.GetXenStoreItem("device/vbd").children)
                    {
                        if (wmisession.GetXenStoreItem(device + "/device-type").value.Equals("disk"))
                        {
                            if (wmisession.GetXenStoreItem(device + "/target-id").value.Equals(diskNumber.ToString()))
                            {
                                string backend = wmisession.GetXenStoreItem(device + "/backend").value;
                                return wmisession.GetXenStoreItem(backend + "/dev").value;
                            }
                            else
                            {
                                WmiBase.Singleton.DebugMsg("could not find target-id for disk " + diskNumber.ToString());
                            }
                        }
                        else
                        {
                            WmiBase.Singleton.DebugMsg("could not find device-type for " + device);
                        }
                    }
                    throw new Exception("Unable to find disk name");
                }

                WmiSession wmisession;
                List<string> getExtentNames(Win32Impl.Extents extents)
                {
                    List<string> extentnames = new List<string>();
                    WmiBase.Singleton.DebugMsg("There are " + extents.Count.ToString() + " extents");
                    foreach (Win32Impl.Extent extent in extents)
                    {
                        try
                        {
                            WmiBase.Singleton.DebugMsg("Find disk name for extent " + extent.DiskNumber.ToString());
                            string disk = findDiskName(extent.DiskNumber);
                            extentnames.Add(disk);
                        }
                        catch { }// unable to find a matching disk 
                    }
                    return extentnames;
                }
                string path;
                public StoredVolume(Win32Impl.Volume vol, WmiSession wmisession, long nextid)
                {
                    try
                    {
                        Id = nextid;
                        this.wmisession = wmisession;
                        WmiBase.Singleton.DebugMsg("Stored volume: new " + vol.Name);
                        path = getVolumePath();
                        WmiBase.Singleton.DebugMsg("creating " + path + "/name");
                        name = wmisession.GetXenStoreItemCached(path + "/name");
                        WmiBase.Singleton.DebugMsg(" : " + name.ToString());
                        size = wmisession.GetXenStoreItemCached(path + "/size");
                        free = wmisession.GetXenStoreItemCached(path + "/free");
                        volumeName = wmisession.GetXenStoreItemCached(path + "/volume_name");

                        filesystem = wmisession.GetXenStoreItemCached(path + "/filesystem");
                    }
                    catch
                    {
                        throw;
                    }
                    try
                    {
                        name.value = vol.Name;
                        size.value = vol.SizeBytes.ToString();
                        freebytes = vol.FreeBytes;
                        free.value = freebytes.ToString();
                        volumeName.value = vol.VolumeName;
                        filesystem.value = vol.FSName;
                    }
                    catch
                    {
                        throw;
                    }
                    try
                    {

                        mountPoints = new List<XenStoreItemCached>();
                        int i = 0;
                        foreach (string mountpoint in vol.pathnames)
                        {
                            XenStoreItemCached mp = wmisession.GetXenStoreItemCached(path + "/mount_points/" + i.ToString());
                            mp.value = mountpoint;
                            mountPoints.Add(mp);
                            i++;
                        }
                    }
                    catch
                    {
                        throw;
                    }

                    try
                    {
                        int i = 0;
                        extents = new List<XenStoreItemCached>();
                        WmiBase.Singleton.DebugMsg("About to iterrate through extent names");
                        foreach (string disk in getExtentNames(vol.extents))
                        {
                            XenStoreItemCached ext = wmisession.GetXenStoreItemCached(path + "/extents/" + i.ToString());
                            ext.value = disk;
                            extents.Add(ext);
                        }
                    }
                    catch
                    {
                        throw;
                    }
                    


                }

                public StoredVolume Change(Win32Impl.Volume vol)
                {
                    WmiBase.Singleton.DebugMsg("changing stored volume id " + Id.ToString());
                    WmiBase.Singleton.DebugMsg("name : "+ name.ToString());
                    WmiBase.Singleton.DebugMsg("size : " + size.ToString());
                    try
                    {
                        name.value = vol.Name;
                        size.value = vol.SizeBytes.ToString();
                        if ((ulong)Math.Abs((long)(vol.FreeBytes - freebytes)) > (vol.SizeBytes / 100))
                        {
                            freebytes = vol.FreeBytes;
                            free.value = freebytes.ToString();
                        }
                        volumeName.value = vol.VolumeName;
                        filesystem.value = vol.FSName;
                    }
                    catch {
                        throw;
                    }
                    try
                    {
                        List<XenStoreItemCached>.Enumerator storecursor = mountPoints.GetEnumerator();
                        List<string>.Enumerator newcursor = vol.pathnames.GetEnumerator();
                        int i = 0;
                        while (true)
                        {
                            if (!storecursor.MoveNext())
                            {
                                while (newcursor.MoveNext())
                                {
                                    XenStoreItemCached mp = wmisession.GetXenStoreItemCached(path + "/mount_points/" + i.ToString());
                                    mp.value = newcursor.Current;

                                    mountPoints.Add(mp);
                                    i++;
                                }
                                break;
                            }
                            else if (!newcursor.MoveNext())
                            {
                                do
                                {
                                    storecursor.Current.Remove();
                                } while (storecursor.MoveNext());
                                break;
                            }
                            else
                            {
                                storecursor.Current.value = newcursor.Current;
                                i++;
                            }
                        }
                    }
                    catch
                    {
                        throw;
                    }
                    try
                    {
                        List<XenStoreItemCached>.Enumerator storecursor = extents.GetEnumerator();
                        List<string>.Enumerator newcursor = getExtentNames(vol.extents).GetEnumerator();
                        int i = 0;
                        while (true)
                        {
                            if (!storecursor.MoveNext())
                            {
                                while (newcursor.MoveNext())
                                {
                                    XenStoreItemCached ext = wmisession.GetXenStoreItemCached(path + "/extents/" + i.ToString());
                                    ext.value = newcursor.Current;
                                    mountPoints.Add(ext);
                                    i++;
                                }
                                break;
                            }
                            else if (!newcursor.MoveNext())
                            {
                                do
                                {
                                    storecursor.Current.Remove();
                                } while (storecursor.MoveNext());
                                break;
                            }
                            else
                            {
                                storecursor.Current.value = newcursor.Current;
                                i++;
                            }
                        }
                    }
                    catch
                    {
                        throw;
                    }
                    return this;

                }
                public void Remove()
                {
                    wmisession.GetXenStoreItem(path).Remove();
                }

            }
        }



        WmiSession wmisession;


        StoredVolumes storedVolumes;
        public VolumeInfo()
        {
            wmisession = WmiBase.Singleton.GetXenStoreSession("Volume Management");
            storedVolumes = new StoredVolumes(wmisession);
        }


        public bool Refresh(bool force)
        {
            Win32Impl.Volumes newvols = new Win32Impl.Volumes();
            bool needkick = storedVolumes.update(newvols);
            refreshCount = 0;
            return needkick;
        }

        protected virtual void Finish()
        {

            
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
        ~VolumeInfo()
        {
            Dispose(false);
        }


    }
}
