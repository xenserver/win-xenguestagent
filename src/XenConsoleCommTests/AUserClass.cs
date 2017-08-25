using IXenConsoleComm;
using System;

namespace XenConsoleComm.Tests.Helpers
{
    public class AUserClass : IXenConsoleEventListener
    {
        private IXenConsoleStream _xcStream;
        private int _xcMessageHandlerCalled = 0;
        private int _disconnectHandlerCalled = 0;

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
            _xcStream.Disconnected += new EventHandler(XenConsoleDisconnectedEventHandler);
        }

        public void DetachFromXenConsoleStream()
        {
            if (_xcStream == null)
                return;

            _xcStream.MessageReceived -= new EventHandler(XenConsoleMessageEventHandler);
            _xcStream.Disconnected -= new EventHandler(XenConsoleDisconnectedEventHandler);
            _xcStream = null;
        }

        public void XenConsoleMessageEventHandler(object sender, EventArgs e)
        {
            ++_xcMessageHandlerCalled;
        }

        public void XenConsoleDisconnectedEventHandler(object sender, EventArgs e)
        {
            ++_disconnectHandlerCalled;
        }

        public int XCMessageHandlerCalled
        {
            get { return _xcMessageHandlerCalled; }
        }

        public int DisconnectHandlerCalled
        {
            get { return _disconnectHandlerCalled; }
        }
    }
}