using IXenConsoleComm;
using System;
using System.Collections.Generic;
using System.Text;

namespace XenConsoleComm.Tests.Helpers
{
    public class AUserClass : IXenConsoleEventListener
    {
        private IXenConsoleStream _xcStream;
        private int _xcMessageHandlerCalled = 0;
        private int _disconnectHandlerCalled = 0;

        public List<byte[]> readMessage;

        public AUserClass(IXenConsoleStream xcStream)
        {
            AttachToXenConsoleStream(xcStream);
            readMessage = new List<byte[]>();
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
            XenConsoleMessageEventArgs args = (XenConsoleMessageEventArgs)e;
            readMessage.Add(UTF8Encoding.UTF8.GetBytes(args.Value));
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