using IXenConsoleComm;
using System;
using XenConsoleComm.Interfaces;
using System.IO.Pipes;

namespace XenConsoleComm
{
    internal class XenConsoleMessageEventArgs : EventArgs, IXenConsoleMessageEventArgs
    {
        private readonly string _message;
        private readonly INamedPipeClientStream _pipeStream;
        private bool _canReply = true;

        public XenConsoleMessageEventArgs(string message, INamedPipeClientStream pipeStream)
        {
            _message = message;
            _pipeStream = pipeStream;
        }

        public string Value
        {
            get { return _message; }
        }

        public void Reply(string value)
        {
            if (!_canReply)
                throw new InvalidOperationException(
                    "'Reply' can be called at most once."
                );

            _canReply = false;
            _pipeStream.Write(value);
        }
    }
}
