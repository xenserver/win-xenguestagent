using NUnit.Framework;
using System;
using System.IO;
using XenConsoleComm.Tests.Stubs;
using XenConsoleComm.Wrappers;

namespace XenConsoleComm.Tests
{
    [TestFixture]
    public class XenConsoleMessageEventArgsTests
    {
        [Test]
        public void Reply_CalledOnce_Ok()
        {
            // Arrange
            NamedPipeClientStreamStub pipeStream = new NamedPipeClientStreamStub();
            XenConsoleMessageEventArgs message = new XenConsoleMessageEventArgs(
                "test",
                new NamedPipeClientStreamWrapper(pipeStream)
            );

            // Assert
            Assert.That(() =>
                message.Reply("reply"), // Act
                Throws.Nothing
            );
        }

        [Test]
        public void Reply_CalledTwice_InvalidOperationExceptionThrown()
        {
            // Arrange
            NamedPipeClientStreamStub pipeStream = new NamedPipeClientStreamStub();
            XenConsoleMessageEventArgs message = new XenConsoleMessageEventArgs(
                "test",
                new NamedPipeClientStreamWrapper(pipeStream)
            );

            // Assert
            Assert.That(() =>
                { message.Reply("first"); message.Reply("second"); }, // Act
                Throws.Exception
                    .TypeOf<InvalidOperationException>()
                    .With.Property("Message")
                    .EqualTo("'Reply' can be called at most once.")
            );

        }

        [Test]
        public void Reply_PipeBroken_IOExceptionThrown()
        {
            // Arrange
            NamedPipeClientStreamStub pipeStream = new NamedPipeClientStreamStub {
                PipeIsBroken = true
            };

            XenConsoleMessageEventArgs message = new XenConsoleMessageEventArgs(
                "test",
                new NamedPipeClientStreamWrapper(pipeStream)
            );

            // Assert
            Assert.That(() =>
                message.Reply("reply"), // Act
                Throws.Exception
                    .TypeOf<IOException>()
                    .With.Property("Message")
                    .EqualTo("Pipe is broken.")
            );
        }
    }
}
