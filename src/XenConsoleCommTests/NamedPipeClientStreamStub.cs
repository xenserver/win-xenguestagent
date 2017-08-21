using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using XenConsoleComm.Interfaces;
using XenConsoleComm.Tests.Helpers;

namespace XenConsoleComm.Tests.Stubs
{
    internal class NamedPipeClientStreamStub : INamedPipeClientStream
    {
        private static readonly Random Rnd = new Random();
        public static readonly UTF8Encoding UTF8Enc = new UTF8Encoding(false);
        public static readonly int BufferSize = 1024; // Read/Write (bytes)

        public List<byte[]> chunksWritten = new List<byte[]>();
        public List<byte[]> chunksRead = new List<byte[]>();
        public ThreadSafeDict<string, int> asyncReads = new ThreadSafeDict<string, int>();

        private bool _pipeIsClosed = false;
        private bool _pipeIsBroken = false;
        private bool _canRead = true;
        private bool _isMessageComplete = false;
        private bool _isConnected = false;
        private bool _pipeServerIsReadWrite= true;
        private bool _invokeCallback = false;

        private string[] _readsReturn;
        private int _readsCompleted = 0;
        private Exception _callbackException = null;

        public void Connect()
        {
            if (IsConnected)
                throw new InvalidOperationException("The client is already connected.");
            if (!PipeServerIsReadWrite)
                throw new UnauthorizedAccessException("Access to the path is denied.");

            IsConnected = true;
        }

        public void Dispose() { }

        public IAsyncResult BeginRead(
            byte[] buffer,
            int offset,
            int count,
            AsyncCallback callback,
            Object state)
        {
            if (buffer == null)
                throw new ArgumentNullException();
            if (offset < 0 || count < 0)
                throw new ArgumentOutOfRangeException();
            if (count > buffer.Length)
                throw new ArgumentException();
            if (PipeIsClosed)
                throw new ObjectDisposedException("", "Cannot access a closed pipe.");
            if (!CanRead)
                throw new NotSupportedException();
            if (PipeIsBroken)
                throw new IOException("Pipe is broken.");

            string id = Rnd.Next().ToString() + Rnd.Next().ToString();
            asyncReads[id] = 0;

            AsyncResultStub ars = new AsyncResultStub(id);

            if (_readsReturn != null && _readsCompleted < _readsReturn.Length)
            {
                Thread thread = new Thread(() =>
                    ThreadWorker(_readsReturn[_readsCompleted], buffer, callback, ars)
                );
                thread.Start();
            }

            return ars;
        }

        public int EndRead(IAsyncResult asyncResult)
        {
            AsyncResultStub ars = (AsyncResultStub)asyncResult;
            // All keys of the hashmnap must have value '1' in the end.
            ++asyncReads[ars.Id];
            return ars.BytesWritten;
        }

        public void Write(string value)
        {
            if (PipeIsBroken)
                throw new IOException("Pipe is broken.");

            byte[] buffer = UTF8Enc.GetBytes(value);

            int bytesLeft = buffer.Length;
            int index = 0;

            while (bytesLeft > 0)
            {
                int count = bytesLeft < BufferSize
                    ? bytesLeft
                    : BufferSize;

                byte[] chunk = new byte[BufferSize];
                Array.Copy(buffer, index, chunk, 0, count);
                chunksWritten.Add(chunk);
                bytesLeft -= count;
                index += count;
            }
        }

        private void ThreadWorker(string value, byte[] buffer, AsyncCallback callback, AsyncResultStub ars)
        {
            byte[] valueBytes = UTF8Enc.GetBytes(value);

            ars.IsCompleted = true;
            ars.BytesWritten = valueBytes.Length < BufferSize
                ? valueBytes.Length
                : BufferSize;

            Array.Copy(
                valueBytes,
                buffer,
                ars.BytesWritten
            );

            chunksRead.Add(new byte[BufferSize]);
            buffer.CopyTo(chunksRead.Last(), 0);

            ++_readsCompleted;
            IsMessageComplete = (valueBytes.Length <= BufferSize);

            if (InvokeCallback)
            {
                try
                {
                    callback.Invoke(ars);
                }
                catch (Exception exc)
                {
                    _callbackException = exc;
                }
            }
        }

        public bool PipeIsClosed
        {
            get { return _pipeIsClosed; }
            set { _pipeIsClosed = value; }
        }

        public bool PipeIsBroken
        {
            get { return _pipeIsBroken; }
            set { _pipeIsBroken = value; }
        }

        public bool CanRead
        {
            get { return _canRead; }
            set { _canRead = value; }
        }

        public bool IsMessageComplete
        {
            get { return _isMessageComplete; }
            set { _isMessageComplete = value; }
        }

        public bool IsConnected
        {
            get { return _isConnected; }
            set { _isConnected = value; }
        }

        public bool PipeServerIsReadWrite
        {
            get { return _pipeServerIsReadWrite; }
            set { _pipeServerIsReadWrite = value; }
        }

        public bool InvokeCallback
        {
            get { return _invokeCallback; }
            set { _invokeCallback = value; }
        }

        public string[] ReadsReturn
        {
            set { _readsReturn = value; }
        }

        public int ReadsCompleted
        {
            get { return _readsCompleted; }
        }

        public Exception CallbackException
        {
            get { return _callbackException; }
        }
    }
}
