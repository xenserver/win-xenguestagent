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
using System.Linq;
using System.Text;
using System.IO.Pipes;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
namespace XenGuestLib
{
    public interface ICommunicator
    {
        void HandleFailure(string reason);
        void HandleSetClipboard(string value);
        void HandleConnected(Communicator client);
    }

    abstract public class Communicator
    {
        PipeStream Pipe;
        protected const string PIPENAME =  "CitrixXenGuestAgent";
        object comlock;
        byte[] buffer;
        int bufferreadpos;
        int bufferstartreadpos;
        int buffersize;
        ICommunicator callbacks;
        bool connected = false;
        public const byte SET_CLIPBOARD = 1;
        public const byte CONNECT = 2;
        public const byte CONNECTED = 3;
        public const byte DISCONNECT = 4;
        public const byte PING = 5;
        public const byte WIPE_CLIPBOARD = 6;

        abstract public void HandleMsgConnect(string secret);
        abstract public void HandleMsgConnected(string value);

        public Communicator(ICommunicator callbacks)
        {
            this.callbacks = callbacks;
            comlock = new object();
        }

        public void SetPipe(PipeStream Pipe)
        {
            this.Pipe = Pipe;
        }

        public void SetupReader()
        {
            this.bufferreadpos = 0;
            this.bufferstartreadpos = 0;
            buffersize = 1024*1024;
            buffer = new byte[buffersize];
            if (!Pipe.IsConnected) {
                onFailure("Pipe is missing");
                return;
            }
            try {
                Pipe.BeginRead(this.buffer, 0, this.buffersize, HandleRead, this);
            }
            catch {
                onFailure("Pipe is missing on BeginRead");
                throw;
            }
        }

        public void onFailure(string why) {
            callbacks.HandleFailure(why);
        }

        virtual public void HandleMsgUnknown(int messagetype, string value)
        {
        }

        void processData(int bytes)
        {
            Debug.Print("Process data " + bytes.ToString());
            
            bufferreadpos +=bytes;

            while (bufferreadpos > bufferstartreadpos)
            {
                Debug.Print("Buffer to process is " +( bufferreadpos-bufferstartreadpos).ToString());
                MemoryStream ms = new MemoryStream(this.buffer, bufferstartreadpos, bufferreadpos-bufferstartreadpos);
                BinaryReader br = new BinaryReader(ms);
                byte messagetype = br.ReadByte();
                if (!connected)
                {

                    if (messagetype == CONNECT)
                    {
                        Debug.Print("Message:Connect");
                        string cstring = br.ReadString();
                        HandleMsgConnect(cstring);
                        connected = true;
                        callbacks.HandleConnected(this);
                    }
                    else if (messagetype == CONNECTED)
                    {
                        Debug.Print("Message:Connected");
                        string cstring = br.ReadString();
                        connected = true;
                        HandleMsgConnected(cstring);

                    }
                    else if (messagetype == PING) {
                        string cstring = br.ReadString();
                        Debug.Print("PING message:" + cstring);
                    }
                    else
                    {
                        Debug.Print("Message:Unknown (connect phase)");
                        string cstring = br.ReadString();
                        /* Unknown message*/
                        HandleMsgUnknown(messagetype, cstring);
                    }
                }
                else
                {
                    if (messagetype == SET_CLIPBOARD)
                    {
                        Debug.Print("Message:SetClipboard");
                        string cstring = br.ReadString();
                        callbacks.HandleSetClipboard(cstring);
                    }
                    else if (messagetype == WIPE_CLIPBOARD)
                    {
                        Debug.Print("Message:WipeClipboard");
                        callbacks.HandleSetClipboard("");
                    }
                    else if (messagetype == PING) {
                        string cstring = br.ReadString();
                        Debug.Print("PING message:" + cstring);
                    }
                    else
                    {
                        Debug.Print("Message:Unknown (post connect phase)");
                        string cstring = br.ReadString();
                        /* Unknown message*/
                        HandleMsgUnknown(messagetype, cstring);
                    }
                }
                Debug.Print("Removing from buffer size " + ms.Position.ToString());
                bufferstartreadpos += (int)ms.Position;
                if (bufferreadpos > bufferstartreadpos) {
                    Buffer.BlockCopy(buffer, bufferstartreadpos, buffer, 0, bufferreadpos - bufferstartreadpos);
                    bufferreadpos -= bufferstartreadpos;
                    bufferstartreadpos = 0;
                }
                else {
                    bufferreadpos=0;
                    bufferstartreadpos=0;
                }
            }

        }

        void HandleRead(IAsyncResult ar)
        {
            lock (comlock)
            {
                int bytes;
                try
                {
                    bytes = Pipe.EndRead(ar);
                    if (bytes == 0)
                    {
                        Debug.Print(ar.ToString());
                        Debug.Print(ar.CompletedSynchronously.ToString());
                        Debug.Print(ar.IsCompleted.ToString());
                        onFailure("No bytes to read, pipe closed");
                        return;
                    }
                }
                catch (Exception e)
                {
                    Debug.Print(string.Format("End Read Failed {0}", e.ToString()));
                    onFailure("End read threw exception " + e.ToString());
                    return;
                }

                try
                {
                    processData(bytes);

                    try {
                        Pipe.BeginRead(buffer, bufferreadpos, buffersize - bufferreadpos, HandleRead, this);
                    }
                    catch {
                        onFailure("Pipe broken");
                        return;
                    }
                }
                catch (EndOfStreamException)
                {
                    if (bufferreadpos == buffersize)
                    {
                        buffersize = buffersize * 2;
                        byte[] newbuffer = new byte[buffersize];
                        buffer.CopyTo(newbuffer, 0);
                        buffer = newbuffer;
                    }
                    try
                    {
                        Debug.Print("new read " + bufferreadpos.ToString() + " " + (buffersize - bufferreadpos).ToString());
                        if (!Pipe.CanRead)
                        {
                            Debug.Print("For some reason I can't read from this pipe.  Ideas?");
                        }
                        if (!Pipe.IsConnected) {
                            onFailure("Pipe has gone away");
                            return;
                        }
                        try {
                            Pipe.BeginRead(buffer, bufferreadpos, buffersize - bufferreadpos, HandleRead, this);
                        }
                        catch {
                            onFailure("Pipe broken");
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.Print("Communicator found too little to read : " + buffersize.ToString() + " " + bufferreadpos.ToString() + " " + e.ToString());
                        onFailure("Insufficient data to read");
                        return;
                    }
                }
                catch (Exception e)
                {
                    Debug.Print("Communicator failure :" + e.ToString());
                    onFailure("Handle read comms failure : " + e.ToString());
                    return;
                }
            }
        }

        public void CloseMessagePipes()
        {
            Debug.Print("Communicator closing message pipes");
            this.Pipe.Close();
            this.Pipe.Dispose();

        }

        public void SendMessage(byte message){
            Debug.Print("SendMsg nb");
            lock (comlock) {
                if (connected && message == WIPE_CLIPBOARD) {
                            Debug.Print("Send valid message (no body): " + message.ToString());
                            BinaryWriter bw = new BinaryWriter(Pipe);
                            bw.Write(message);
                }
            }
        }
        public void SendMessage(byte message, string value)
        {

            lock (comlock)
            {
                try {
                    if (connected || (message == CONNECT) || (message == CONNECTED) )
                    {
                        Debug.Print("Send valid message: " + message.ToString()+" "+value);
                        BinaryWriter bw = new BinaryWriter(Pipe);
                        bw.Write(message);
                        bw.Write(value);
                    }
                }
                catch (Exception e) {
                    Debug.Print("Communicator Write failure :" + e.ToString());
                    onFailure("Handle read comms failure : " + e.ToString());
                }
            }
        }
    }


    public interface ICommClientImpl : ICommunicator
    {
        
    }
    public class CommClient : Communicator
    {
        NamedPipeClientStream Pipe;
        bool connecting = false;
        ICommClientImpl callbacks;
        ManualResetEvent connectevent;
        public CommClient(ICommClientImpl callbacks, string localsecret) : base(callbacks)
        {
            Pipe = new NamedPipeClientStream(
                ".", PIPENAME, PipeDirection.InOut,
                PipeOptions.Asynchronous);
            SetPipe(Pipe);
            this.callbacks = callbacks;
            connectevent = new ManualResetEvent(false);
            connecting = true;
            try
            {
                Pipe.Connect(2000);
                SetupReader();
                SendMessage(Communicator.CONNECT, localsecret);
                connectevent.WaitOne();
            }
            catch (Exception e)
            {
                Debug.Print("CLIENT constructor failure: " +e.ToString());
                throw;
            }
            
        }
        override public void HandleMsgConnected(string message)
        {
            if ((!connecting) || (message == "Failure"))
            {
                Debug.Print("Connection failed");
                CloseMessagePipes();
                onFailure("Conection failed (not accepted)");
                connectevent.Set();
                return;
            }
            if (message == "Success")
            {
                callbacks.HandleConnected(this);
                connectevent.Set();
            }
        }
        override public void HandleMsgConnect(string secret)
        {
            CloseMessagePipes();
            Debug.Print("Connection rejected");
            onFailure("Connection rejected");
        }
    }
    public interface ICommServerImpl : ICommunicator
    {

    }
    public class CommServer : Communicator
    {
        public string secret;
        NamedPipeServerStream Pipe;
        ICommServerImpl callbacks;
        public CommServer(ICommServerImpl callbacks) : base(callbacks)
        {
            int numsecretbytes=16;
            byte[] secretbytes = new byte[numsecretbytes];
            RNGCryptoServiceProvider rnd = new RNGCryptoServiceProvider();
            int i;
            rnd.GetBytes(secretbytes);
            secret="";
            for (i = 0; i < numsecretbytes; i++)
            {
                secret = secret + String.Format("{0:x2}", secretbytes[i]);
            }
            PipeSecurity sec = new PipeSecurity();
            sec.AddAccessRule(
                    new PipeAccessRule( 
                        new System.Security.Principal.SecurityIdentifier(
                            System.Security.Principal.WellKnownSidType.WorldSid, 
                            null), 
                        PipeAccessRights.ReadWrite,
                        System.Security.AccessControl.AccessControlType.Allow));
            sec.AddAccessRule(new PipeAccessRule(System.Security.Principal.WindowsIdentity.GetCurrent().Name, PipeAccessRights.FullControl,
                System.Security.AccessControl.AccessControlType.Allow));

            Pipe = new NamedPipeServerStream(PIPENAME,
                PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous, 1024*1024, 1024*1024, sec);
            SetPipe(Pipe);
            this.callbacks = callbacks;
            Pipe.BeginWaitForConnection(HandleConnected, this);
        }
        void HandleConnected(IAsyncResult ar)
        {
            try
            {
                Pipe.EndWaitForConnection(ar);
                SetupReader();
                
            }
            catch (ObjectDisposedException)
            {
                // Ignore - Pipe has gone away
            }
            catch (OperationCanceledException) {
                // Ignore - we closed the pipe before we ever opened it
            }
            catch (Exception e) {
                // In the event of an IO Exception
                Debug.Print("Unexpected exception when connecting to depriv client: "+e.ToString());
            }
        }
        override public void HandleMsgConnect(string secret)
        {
            if (secret == this.secret)
            {
                SendMessage(Communicator.CONNECTED, "Success");
            }
            else
            {
                SendMessage(Communicator.CONNECTED, "Failure");
                Debug.Print("Server Connect Failure");
                onFailure("Server connect failure");
            }
        }
        public override void HandleMsgConnected(string value)
        {
            Debug.Print("Should not receive msg connected");
            onFailure("HandleMsgConnected should not be received");
        }
        public string GetSecret()
        {
            return secret;
        }

        new public void CloseMessagePipes()
        {
            Debug.Print("Close server pipe");
            if (Pipe.IsConnected) {
                this.Pipe.Disconnect();
            }
            base.CloseMessagePipes();
        }
    }
}
