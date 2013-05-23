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
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using XenGuestLib;
using System.Diagnostics;
using System.Threading;

namespace svc_depriv
{
    public partial class DummyForm : Form, ICommClientImpl
    {
        string secret;
        Communicator client;
        ClipboardChain clipchain;

        public DummyForm(string secret)
        {
            this.secret = secret;
            clipchain = new ClipboardChain();
            this.Controls.Add(clipchain);
            InitializeComponent();
            clipboardtext = Clipboard.GetText();
            client = new CommClient(this, secret);

        }

        string clipboardtext="";

        class SetClipboardText
        {
            string value;
            ManualResetEvent done = null;
            public SetClipboardText(string value)
            {
                this.value = value;
            }
            public void Set()
            {
                try {
                    if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
                    {
                        done = new ManualResetEvent(false);
                        Thread t = new Thread(new ThreadStart(Set));
                        t.SetApartmentState(ApartmentState.STA);
                        t.Start();
                        done.WaitOne();
                    }
                    else
                    {



                        if ("".Equals(this.value))
                        {
                            Debug.Print("Setting (null) clipboard text");
                            Clipboard.Clear();
                        }
                        else
                        {
                            Debug.Print("Setting clipboard text to " + value);
                            int count = 0;
                            while (count < 5)
                            {
                                try
                                {
                                    Clipboard.SetText(value);
                                    break;
                                }
                                catch
                                {
                                    System.Threading.Thread.Sleep(100);
                                    count++;
                                }
                            }
                        }
                        if (done != null)
                        {
                            done.Set();
                        }
                    }
                }
                catch (Exception e) {
                    Debug.Print("Clipboard setting thread exception "+e.ToString());
                    throw;
                }
            }
        }
        void ICommunicator.HandleConnected(Communicator client)
        {
            try {
                Debug.Print("Received HandleConnected");
                this.client = client;
                clipchain.ClipboardChanged += ClipboardChanged;
            }
            catch (Exception e)
            {
                Debug.Print("BAD connected: " + e.ToString());
                throw;
            }
        }
        void ICommunicator.HandleSetClipboard(string value)
        {
            try
            {
                if (!value.Equals(clipboardtext))
                {
                    clipboardtext = value;
                    (new SetClipboardText(value)).Set();
                    Debug.Print("Clipboard changed");
                }
            }
            catch (Exception e)
            {
                Debug.Print("BAD Clipboard Set: " + e.ToString());
                throw;
            }
        }

        void ICommunicator.HandleFailure(string reason) {
            Trace.WriteLine("Depriv client failure: " + reason);
            Debug.Print((new System.Diagnostics.StackTrace()).ToString());
            clipchain.Remove();
        }


        private void UpdateClipboard()
        {            Debug.Print("Callback");            if (Clipboard.ContainsText()) {                Debug.Print("Text");                string newclipboard = Clipboard.GetText();                if (!clipboardtext.Equals(newclipboard))                {                    Debug.Print("Changed");                    clipboardtext = newclipboard;                    if (newclipboard == String.Empty) {                        client.SendMessage(Communicator.WIPE_CLIPBOARD);                    }                    else {                        client.SendMessage(Communicator.SET_CLIPBOARD, newclipboard);                    }                }
             }            else {                if (clipboardtext != String.Empty) {                    client.SendMessage(Communicator.WIPE_CLIPBOARD);                    clipboardtext = String.Empty;                }            }            Debug.Print("Done");
        }
        private void ClipboardChanged(Object sender, ClipboardChangedEventArgs data)
        {
            try
            {
                UpdateClipboard();
            }
            catch (Exception e)
            {
                Debug.Print("Update clipboard failed: " + e.ToString());
                throw;
            }
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            Hide();
        }
    }
}
