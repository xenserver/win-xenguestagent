using System;

namespace XenConsoleComm.Interfaces
{
    internal interface INamedPipeClientStream
    {
        void Connect();
        IAsyncResult BeginRead(
            byte[] buffer,
            int offset,
            int count,
            AsyncCallback callback,
            object state
        );
        int EndRead(IAsyncResult asyncResult);
        void Write(string value);

        bool IsMessageComplete { get; }
        bool IsConnected { get; }
    }
}
