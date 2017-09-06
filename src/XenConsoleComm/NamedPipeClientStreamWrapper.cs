using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using XenConsoleComm.Interfaces;

namespace XenConsoleComm.Wrappers
{
    public delegate void connectFn();

    internal class NamedPipeClientStreamWrapper : INamedPipeClientStream
    {

        enum MessageStatus
        {
            NotStarted,
            InProgress
        };
        private MessageStatus status = MessageStatus.NotStarted;
        private byte[] readBuffer;
        private AsyncCallback callback;
        private int userOffset;
        private int userByteCount;
        byte[] userBuffer;
        private object state;
        private byte[] cache = new byte[0];
        private int cacheByteCount = 0;
        private bool _isMessageComplete;
        private AsyncMessageResultWrapper asyncMessage;
        private PipeStream _pipeStream;
        private static readonly UTF8Encoding UTF8Enc = new UTF8Encoding(false);

        // A 0-length UTF8 string may still occupy some bytes
        private static readonly int UTF8FixedCost =
            UTF8Enc.GetMaxByteCount(0);

        // Maximum number of bytes a UTF8 char can occupy
        private static readonly int UTF8MaxCharSize =
            UTF8Enc.GetMaxByteCount(1) - UTF8FixedCost;

        // Max number of bytes to send over pipe stream
        private static readonly int WriteBufferSize = 1024;

        // Largest length of a string encoded in UTF8 that can
        // comfortably fit in a 'byte[WriteBufferSize]' array.
        private static readonly int MaxCharCount =
            (WriteBufferSize - UTF8FixedCost) / UTF8MaxCharSize;

        private NamedPipeClientStreamWrapper() { }

        public NamedPipeClientStreamWrapper(
            NamedPipeClientStream namedPipeClientStream)
        {
            _pipeStream = namedPipeClientStream;
        }

        
        connectFn _connectFn = null;
        public NamedPipeClientStreamWrapper(
            string serverName,
            string pipeName,
            PipeDirection direction,
            PipeOptions options)
        {
            NamedPipeClientStream pipe = new NamedPipeClientStream(
                serverName,
                pipeName,
                direction,
                options
            );
            _connectFn = pipe.Connect;
            _pipeStream = pipe;
        }

        public NamedPipeClientStreamWrapper(
            PipeStream wrappedPipeStream,
            connectFn connectCallback = null)
        {
            _pipeStream = wrappedPipeStream;
            _connectFn = connectCallback;
        }


        public void Connect()
        {
            if (_pipeStream.IsConnected)
            {
                throw new InvalidOperationException("The client is already connected.");
            }
            if (_connectFn != null) {
                _connectFn();
                _pipeStream.ReadMode = PipeTransmissionMode.Message;
            }
            
        }

        public void Dispose()
        {
            _pipeStream.Dispose();
        }

        private bool MessageNotStarted(int bytes)
        {
            int start = 0;

            start = Array.IndexOf<byte>(readBuffer, 0x02, 0, bytes);
            if (start != -1)
            {
                return (MessageInProgress(start + 1, bytes - (start + 1)));
            }

            ReadMore(userByteCount);
            return false;
        }

        private bool MessageInProgress(int readOffset, int bytes)
        {
            status = MessageStatus.InProgress;

            int end = Array.IndexOf<byte>(readBuffer, 0x03,readOffset, bytes);

            if (end == -1) 
            {
                Array.Copy(readBuffer, readOffset, userBuffer, userOffset, bytes);
                userOffset += bytes;
                if (userOffset == userBuffer.GetLength(0)) 
                {
                    IndicateIncompleteMessage(userByteCount);
                    return true;
                }
                else 
                {
                    return ReadMore(userByteCount-userOffset);
                }
            }
            else 
            {
                end = end - readOffset;
                Array.Copy(readBuffer, readOffset, userBuffer, userOffset, end);
                Debug.Assert(cacheByteCount == 0);
                cacheByteCount = bytes - end;
                Array.Resize<byte>(ref cache, cacheByteCount);
                Array.Copy(readBuffer, readOffset + end + 1, cache, 0, cacheByteCount);
                end = end + userOffset;
                userOffset = 0;
                IndicateCompleteMessage(end);
                return true;
            }

        }

        private void IndicateCompleteMessage(int bytesRead)
        {
            _isMessageComplete = true;
            asyncMessage.Indicate(bytesRead, true);
        }

        private void IndicateIncompleteMessage(int bytesRead)
        {
            _isMessageComplete = false;
            asyncMessage.Indicate(bytesRead, false);
        }

        private class AsyncMessageResultWrapper : IAsyncResult
        {
            private bool _IsCompleted;
            private bool _CompletedSynchronously;
            private int _bytesRead;
            private EventWaitHandle _AsyncWaitHandle;
            private AsyncCallback _callback;
 

            public void Indicate(int bytesRead, bool completed)
            {
                _IsCompleted = completed;
                _bytesRead = bytesRead;
                _AsyncWaitHandle.Set();
                _callback(this);
            }

            public int BytesRead 
            {
                get { return _bytesRead; }
            }

            public AsyncMessageResultWrapper(IAsyncResult result, AsyncCallback callback)
            {
                _IsCompleted = result.IsCompleted;
                _CompletedSynchronously = result.CompletedSynchronously;
                _AsyncWaitHandle = new EventWaitHandle(result.IsCompleted, EventResetMode.ManualReset);
                _callback = callback;
            }

            public AsyncMessageResultWrapper(bool synchronously, AsyncCallback callback)
            {
                if (synchronously)
                {
                    _CompletedSynchronously = true;
                    _IsCompleted = true;
                }
                else
                {
                    _CompletedSynchronously = false;
                    _IsCompleted = false;
                }
                _AsyncWaitHandle = new EventWaitHandle(_IsCompleted, EventResetMode.ManualReset);
                _callback = callback;
            }

            public object AsyncState { get { return null; } }
            public WaitHandle AsyncWaitHandle { get { return _AsyncWaitHandle; } }
            public bool CompletedSynchronously { get { return _CompletedSynchronously; } }
            public bool IsCompleted { get { return _IsCompleted; } }
        };

        private void OnBytesRead(IAsyncResult ar)
        {
            int bytesread = _pipeStream.EndRead(ar);
            if (bytesread == 0)
            {
                IndicateIncompleteMessage(userOffset);
                return;
            }
            if (status == MessageStatus.NotStarted) {
                MessageNotStarted(bytesread);
                return;
            }
            else if (status == MessageStatus.InProgress) {
                MessageInProgress(0, bytesread);
                return;
            }
            
        }

        public IAsyncResult BeginRead(
            byte[] buffer,
            int offset,
            int count,
            AsyncCallback callback,
            object state)
        {
            this._isMessageComplete = false;
            this.status = MessageStatus.NotStarted;
            this.readBuffer = new byte[count];
            this.callback = callback;
            this.userOffset = offset;
            this.userByteCount = count;
            this.userBuffer = buffer;
            this.state = state;
            if (cacheByteCount > 0)
            {
                int cacheCopy = Math.Min(cacheByteCount, count);
                Array.Copy(cache, readBuffer, cacheCopy);
                cacheByteCount = cacheByteCount - cacheCopy;
                asyncMessage = new AsyncMessageResultWrapper(MessageNotStarted(cacheCopy), callback);
                return asyncMessage;
            }
            else
            {
                asyncMessage = new AsyncMessageResultWrapper(_pipeStream.BeginRead(readBuffer, 0, count, OnBytesRead, state),callback);
                return asyncMessage;
            }
        }

        private bool ReadMore(int bytesToRead) 
        {
            if (cacheByteCount > 0)
            {
                int cacheCopy = Math.Min(cacheByteCount, bytesToRead);
                Array.Copy(cache, readBuffer, cacheCopy);
                cacheByteCount = cacheByteCount - cacheCopy;
                return MessageNotStarted(cacheCopy);
            }
            else
            {
                _pipeStream.BeginRead(readBuffer, 0, bytesToRead, OnBytesRead, state);
                return false;
            }
        }

        public int EndRead(IAsyncResult asyncResult)
        {
            return ((AsyncMessageResultWrapper)asyncResult).BytesRead;
        }

        public void Write(string value)
        {
            value = '\x02' + value + '\x03'; //It's a message, so wrap it accordingly
            byte[] buffer = new byte[2 * WriteBufferSize];

            int charsLeft = value.Length;
            int byteIdx = 0;
            int charIdx = 0;

            int bytesInBuffer = 0;

            while (charsLeft > 0 || bytesInBuffer > 0)
            {
                int charCount = charsLeft < MaxCharCount
                    ? charsLeft
                    : MaxCharCount;

                byteIdx = bytesInBuffer;

                bytesInBuffer += UTF8Enc.GetBytes(
                    value,
                    charIdx,
                    charCount,
                    buffer,
                    byteIdx
                );

                charsLeft -= charCount;
                charIdx += charCount;

                if (bytesInBuffer < WriteBufferSize && charsLeft > 0)
                    continue;

                int bytesToWrite = bytesInBuffer < WriteBufferSize
                    ? bytesInBuffer
                    : WriteBufferSize;

                _pipeStream.Write(buffer, 0, bytesToWrite);
                _pipeStream.Flush();

                bytesInBuffer -= bytesToWrite;
                if (bytesInBuffer > 0)
                {
                    Array.Copy(
                        buffer,
                        WriteBufferSize,
                        buffer,
                        0,
                        bytesInBuffer
                    );
                }
            }
        }

        public bool IsMessageComplete
        {
            get { return _isMessageComplete; }
        }

        public bool IsConnected
        {
            get { return _pipeStream.IsConnected; }
        }
    }
}