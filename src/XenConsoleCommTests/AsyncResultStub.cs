using System;
using System.Threading;

namespace XenConsoleComm.Tests.Stubs
{
    public class AsyncResultStub : IAsyncResult
    {
        private bool _completedSynchronously = false;
        private bool _isCompleted = false;
        private object _asyncState;
        private string _id;
        private int _bytesWritten = 0;

        public AsyncResultStub(string id) : this(id, null) { }

        public AsyncResultStub(string id, object asyncState)
        {
            _id = id;
            _asyncState = asyncState;
        }

        public string Id
        {
            get { return _id; }
        }

        public int BytesWritten
        {
            get { return _bytesWritten; }
            set { _bytesWritten = value; }
        }

        public bool CompletedSynchronously
        {
            get { return _completedSynchronously; }
            set { _completedSynchronously = value; }
        }

        public object AsyncState
        {
            get { return _asyncState; }
            set { _asyncState = value; }
        }

        public bool IsCompleted
        {
            get { return _isCompleted; }
            set { _isCompleted = value; }
        }

        public WaitHandle AsyncWaitHandle
        {
            get { return null; }
        }
    }
}
