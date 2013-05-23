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
namespace xenwinsvc
{
    public class MemoryInfo : IRefresh
    {
        ulong totalmem = 0;
        ulong freemem = 0;
        WmiSession wmisession;
        XenStoreItem meminfoFree;
        XenStoreItem meminfoTotal;
        public MemoryInfo()
        {
            wmisession = WmiBase.Singleton.GetXenStoreSession("Memory Reporter");
            meminfoFree = wmisession.GetXenStoreItemCached("data/meminfo_free");
            meminfoTotal = wmisession.GetXenStoreItemCached("data/meminfo_total");
        }
        int refreshCount = 0;
        public bool NeedsRefresh()
        {
            refreshCount++;
            if (refreshCount > 26)
            {
                refreshCount = 0;
                return true;
            }
            return false;
        }

        public bool Refresh(bool force)
        {
            refreshCount = 0;
            ulong currentfreemem = (ulong)WmiBase.Singleton.Win32_OperatingSystem["FreePhysicalMemory"];

            if (totalmem == 0)
            {
                totalmem = (ulong)WmiBase.Singleton.Win32_ComputerSystem["TotalPhysicalMemory"] / 1024;
            }

            if (((currentfreemem - freemem) > 1024) ||
                ((freemem - currentfreemem) > 1024) ||
                force)
            {
                if (force) {
                    meminfoFree.value = "";
                    meminfoTotal.value = "";
                }
                meminfoFree.value = currentfreemem.ToString();
                meminfoTotal.value = totalmem.ToString();
                freemem = currentfreemem;
                // return true;
                //FIXME - we never kick after memory changes, as the changes may
                //        be too frequent and xapi / xenopsd may have to do too
                //        many reads.  We should return true if we set any xenstore values
            }

            return false; 
            
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
        ~MemoryInfo()
        {
            Dispose(false);
        }

    }
}
