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

namespace xenwinsvc
{
    public class XenAppSessionInfo : IRefresh
    {
        int sessionCount = 0;
        WmiSession wmisession;
        XenStoreItem xenAppSession;
        public XenAppSessionInfo()
        {
            wmisession = WmiBase.Singleton.GetXenStoreSession("XenAppSession Reporter");
            xenAppSession = wmisession.GetXenStoreItemCached("data/xenapp_session_count");
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
            int currentSessionCount = 0;
            try
            {
                using (RegistryKey XenAppSessionKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Citrix\\Ica\\Session\\CtxSessions"))
                {
                    currentSessionCount = XenAppSessionKey.ValueCount;
                }
            }
            catch(Exception)
            {
                sessionCount = 0;
            }


            if (currentSessionCount != sessionCount || force)
            {
                if (force)
                {
                    xenAppSession.value = "";
                }
                xenAppSession.value = currentSessionCount.ToString();
                sessionCount = currentSessionCount;
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
        ~XenAppSessionInfo()
        {
            Dispose(false);
        }

    }
}
