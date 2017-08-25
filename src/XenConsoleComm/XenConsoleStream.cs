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
        public event EventHandler Disconnected;

        private INamedPipeClientStream _xenConsoleClient;
        private byte[] _readBuffer;
        private Func<string, bool> _messageForwardingRule = null;
        private readonly ushort _readBufferSize = 1024;

        public XenConsoleStream() : this(
            new NamedPipeClientStreamWrapper(
                ".",
                "xencons",
                PipeDirection.InOut,
                PipeOptions.Asynchronous
            )) { }

        internal XenConsoleStream(INamedPipeClientStream pipeClient)
        {
            _xenConsoleClient = pipeClient;
            _readBuffer = new byte[_readBufferSize];
        }

        public void Start()
        {
            if (Disconnected == null)
            {
                throw new InvalidOperationException(
                    "Event 'Disconnected' must have at least "
                    + "1 subscriber before attempting to connect."
                );
            }

            _xenConsoleClient.Connect();
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

        public bool IsConnected
        {
            get
            {
                if (_xenConsoleClient != null)
                    return _xenConsoleClient.IsConnected;
                else
                    return false;
            }
        }

        internal void OnXenConsoleMessageReceived(IAsyncResult ar)
        {
            EventHandler msgReceived = MessageReceived;
            EventHandler discon = Disconnected;
            Func<string, bool> rule = _messageForwardingRule;

            int bytesRead = _xenConsoleClient.EndRead(ar);

            if (bytesRead == 0)
            {
                _xenConsoleClient.Dispose();
                _xenConsoleClient = null;
                _readBuffer = null;
                MessageReceived = null;
                Disconnected = null;

                if (discon != null)
                    discon(this, EventArgs.Empty);

                return;
            }

            if (!_xenConsoleClient.IsMessageComplete)
                throw new NotSupportedException(String.Format(
                    "Message is larger than {0} bytes.",
                    _readBufferSize
                ));

            string message = Encoding.UTF8.GetString(_readBuffer, 0, bytesRead);

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