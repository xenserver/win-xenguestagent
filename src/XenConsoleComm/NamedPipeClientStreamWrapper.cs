using System;
using System.IO.Pipes;
using System.Text;
using XenConsoleComm.Interfaces;

namespace XenConsoleComm.Wrappers
{
    internal class NamedPipeClientStreamWrapper : INamedPipeClientStream
    {
        private NamedPipeClientStream _pipeStream;
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

        public NamedPipeClientStreamWrapper(
            string serverName,
            string pipeName,
            PipeDirection direction,
            PipeOptions options)
        {
            _pipeStream = new NamedPipeClientStream(
                serverName,
                pipeName,
                direction,
                options
            );
        }

        public void Connect()
        {
            _pipeStream.Connect();
            _pipeStream.ReadMode = PipeTransmissionMode.Message;
        }

        public void Dispose()
        {
            _pipeStream.Dispose();
        }

        public IAsyncResult BeginRead(
            byte[] buffer,
            int offset,
            int count,
            AsyncCallback callback,
            object state)
        {
            return _pipeStream.BeginRead(buffer, offset, count, callback, state);
        }

        public int EndRead(IAsyncResult asyncResult)
        {
            return _pipeStream.EndRead(asyncResult);
        }

        public void Write(string value)
        {
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
            get { return _pipeStream.IsMessageComplete; }
        }

        public bool IsConnected
        {
            get { return _pipeStream.IsConnected; }
        }
    }
}