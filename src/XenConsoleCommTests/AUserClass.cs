using IXenConsoleComm;
using System;

namespace XenConsoleComm.Tests.Helpers
{
    public class AUserClass : IXenConsoleEventListener
    {
        private IXenConsoleStream _xcStream;
        private int _xcMessageHandlerCalled = 0;
        private int _pipeDisconnectHandlerCalled = 0;

        public AUserClass(IXenConsoleStream xcStream)
        {
            AttachToXenConsoleStream(xcStream);
        }

        public AUserClass() { }

        public void AttachToXenConsoleStream(IXenConsoleStream xcStream)
        {
            if (_xcStream != null)
                return;

            _xcStream = xcStream;
            _xcStream.MessageReceived += new EventHandler(XenConsoleMessageEventHandler);
            _xcStream.PipeDisconnected += new EventHandler(PipeDisconnectedEventHandler);
        }

        public void DetachFromXenConsoleStream()
        {
            if (_xcStream == null)
                return;

            _xcStream.MessageReceived -= new EventHandler(XenConsoleMessageEventHandler);
            _xcStream.PipeDisconnected -= new EventHandler(PipeDisconnectedEventHandler);
            _xcStream = null;
        }

        public void XenConsoleMessageEventHandler(object sender, EventArgs e)
        {
            ++_xcMessageHandlerCalled;
        }

        public void PipeDisconnectedEventHandler(object sender, EventArgs e)
        {
            ++_pipeDisconnectHandlerCalled;
        }

        public int XCMessageHandlerCalled
        {
            get { return _xcMessageHandlerCalled; }
        }

        public int PipeDisconnectHandlerCalled
        {
            get { return _pipeDisconnectHandlerCalled; }
        }
    }
}