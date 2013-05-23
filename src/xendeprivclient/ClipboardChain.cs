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
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;
using XenGuestLib;

namespace svc_depriv
{// Must inherit Control, not Component, in order to have Handle
    [DefaultEvent("ClipboardChanged")]
    public partial class ClipboardChain : UserControl
    {
        IntPtr nextClipboardViewer;

        public ClipboardChain()
        {
            try
            {
                CreateHandle();
                this.Visible = false;
                Trace.WriteLine("New clipboard viewer");
                nextClipboardViewer = (IntPtr)SetClipboardViewer((int)this.Handle);
            }
            catch (Exception e)
            {
                Debug.Print("Chain viewer: "+ e.ToString());
            }
        }

        /// <summary>
        /// Clipboard contents changed.
        /// </summary>
        public event EventHandler<ClipboardChangedEventArgs> ClipboardChanged;

        public void Remove()
        {
           Trace.WriteLine("Depriv Remove...");

            Trace.WriteLine("Depriv " + this.Handle.ToString() + " " + "Tells chain to relink to " + nextClipboardViewer.ToString() + " " + "it has died");
            xenwinsvc.Win32Impl.SetLastError(0);
            bool res = ChangeClipboardChain(this.Handle, nextClipboardViewer);
            Application.Exit();
            Trace.WriteLine("Depriv Removed");

        }

        [DllImport("User32.dll")]
        protected static extern int SetClipboardViewer(int hWndNewViewer);

        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        public static extern bool ChangeClipboardChain(IntPtr hWndRemove, IntPtr hWndNewNext);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int SendMessage(IntPtr hwnd, int wMsg, IntPtr wParam, IntPtr lParam);

        [System.Security.Permissions.PermissionSet(System.Security.Permissions.SecurityAction.Demand, Name = "FullTrust")]
        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            // defined in winuser.h
            const int WM_DRAWCLIPBOARD = 0x308;
            const int WM_CHANGECBCHAIN = 0x030D;
            try
            {
                switch (m.Msg)
                {
                    case WM_DRAWCLIPBOARD:
                        if (nextClipboardViewer != IntPtr.Zero)
                        {
                            xenwinsvc.Win32Impl.SetLastError(0);
                            SendMessage(nextClipboardViewer, m.Msg, m.WParam, m.LParam);
                            int err = Marshal.GetLastWin32Error();
                            if (err != 0)
                            {
                                Trace.WriteLine("Got error code from forwarding clipboard " + err.ToString() + " " + nextClipboardViewer.ToString());
                            }
                        }
                        OnClipboardChanged();
                        break;

                    case WM_CHANGECBCHAIN:
                        if (m.WParam == nextClipboardViewer)
                        {
                            Trace.WriteLine("Depriv CHANGECBCHAIN : " + nextClipboardViewer.ToString() + " has gone, link to " + m.LParam.ToString());
                            nextClipboardViewer = m.LParam;
                        }
                        else
                        {
                            Trace.WriteLine("Depriv CHANGECBCHAIN : " + nextClipboardViewer.ToString() + " has gone, tell " + m.LParam.ToString());
                            SendMessage(nextClipboardViewer, m.Msg, m.WParam, m.LParam);
                        }
                        break;


                    default:
                        base.WndProc(ref m);
                        break;
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine("Depriv exception " + e.ToString());
            }
        }


        void OnClipboardChanged()
        {

            System.Threading.Thread t = new System.Threading.Thread(() =>
            {
                try
                {
                    if (ClipboardChanged != null)
                    {
                        ClipboardChanged(this, new ClipboardChangedEventArgs(null));
                    }
                }
                catch (Exception e)
                {
                    Debug.Print("Clipbaord onChanged: " + e.ToString());
                }
            });
            t.SetApartmentState(System.Threading.ApartmentState.STA);
            t.Start();
        }
    }

}
