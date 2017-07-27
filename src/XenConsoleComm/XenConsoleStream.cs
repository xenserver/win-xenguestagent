using IXenConsoleComm;
using System;
using System.IO.Pipes;
using System.Text;
using XenConsoleComm.Interfaces;
using XenConsoleComm.Wrappers;

namespace XenConsoleComm
{
    public class XenConsoleStream : IXenConsoleStream
    {
        public event EventHandler MessageReceived;
        public event EventHandler PipeDisconnected;

        private readonly INamedPipeClientStream _xenConsoleClient;
        private byte[] _readBuffer;
        private Func<string, bool> _messageForwardingRule = null;
        private readonly int _readBufferSize = 1024;

        public XenConsoleStream() : this(
            new NamedPipeClientStreamWrapper(
                ".",
                "XenConsoleMonitor",
                PipeDirection.InOut,
                PipeOptions.Asynchronous
            )) { }

        internal XenConsoleStream(INamedPipeClientStream pipeClient)
        {
            _xenConsoleClient = pipeClient;

            if (!_xenConsoleClient.IsConnected)
            {
                _xenConsoleClient.Connect();
            }

            _readBuffer = new byte[_readBufferSize];

            _xenConsoleClient.BeginRead(
                _readBuffer,
                0,
                _readBufferSize,
                new AsyncCallback(OnXenConsoleMessageReceived),
                null
            );
        }

        public Func<string, bool> MessageForwardingRule
        {
            get { return _messageForwardingRule; }
            set { _messageForwardingRule = value; }
        }

        internal void OnXenConsoleMessageReceived(IAsyncResult ar)
        {
            EventHandler msgReceived = MessageReceived;
            EventHandler pipeDiscon = PipeDisconnected;
            Func<string, bool> rule = _messageForwardingRule;

            int bytesRead = _xenConsoleClient.EndRead(ar);

            if (!_xenConsoleClient.IsMessageComplete)
                throw new NotSupportedException(String.Format(
                    "Message is larger than {0} bytes.",
                    _readBufferSize
                ));

            // Console.WriteLine("Message received is {0} bytes.", bytesRead);
            /*
            // TODO: Dispose pipe/make object unusable
            if (bytesRead == 0)
            {
                if (pipeDiscon != null)
                    pipeDiscon(this, EventArgs.Empty);

                return;
            }
            */
            string message = Encoding.UTF8.GetString(_readBuffer, 0, bytesRead).Trim();

            if (msgReceived != null
                    && (rule == null
                        || rule(message)))
            {
                msgReceived(
                    this,
                    new XenConsoleMessageEventArgs(message, _xenConsoleClient)
                );
            }

            _xenConsoleClient.BeginRead(
                _readBuffer,
                0,
                _readBufferSize,
                new AsyncCallback(OnXenConsoleMessageReceived),
                null
            );
        }
    }
}