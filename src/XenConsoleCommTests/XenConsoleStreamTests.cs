using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using XenConsoleComm.Tests.Helpers;
using XenConsoleComm.Tests.Stubs;

namespace XenConsoleComm.Tests
{
    [TestFixture]
    public class XenConsoleStreamTests
    {
        [Test]
        public void Constructor_AsyncReadNotCompleted()
        {
            // Arrange
            NamedPipeClientStreamStub pipeClient = new NamedPipeClientStreamStub();

            // Act
            new XenConsoleStream(pipeClient);

            // Assert
            Assert.AreEqual(0, pipeClient.ReadsCompleted);
            Assert.AreEqual(1, pipeClient.asyncReads.Count);
            foreach (KeyValuePair<string, int> read in pipeClient.asyncReads)
            {
                Assert.AreEqual(0, read.Value);
            }
        }

        [Test]
        public void Constructor_PipeServerIsNotReadWrite_UnauthorizedAccessExceptionThrown()
        {
            // Arrange
            NamedPipeClientStreamStub pipeClient = new NamedPipeClientStreamStub {
                PipeServerIsReadWrite = false
            };

            // Assert
            Assert.That(() =>
                new XenConsoleStream(pipeClient), // Act
                Throws.Exception
                    .TypeOf<UnauthorizedAccessException>()
                    .With.Property("Message")
                    .EqualTo("Access to the path is denied.")
            );
        }

        [Test]
        public void Constructor_AsyncReadCompleted_NoCallback()
        {
            // Arrange
            byte[] tmpBuffer = new byte[NamedPipeClientStreamStub.BufferSize];

            string[] readsReturn = new string[] {
                RandomString.Generate(32),
                RandomString.Generate(17),
                RandomString.Generate(47),
                RandomString.Generate(5),
                RandomString.Generate(23),
            };

            NamedPipeClientStreamStub.UTF8Enc.GetBytes(
                readsReturn[0],
                0,
                readsReturn[0].Length,
                tmpBuffer,
                0
            );

            // With 'pipeClient.InvokeCallback == false',
            // 'BeginRead' will be called exactly once
            // (when initializing the 'XenConsoleStream' constructor),
            // regardless of 'ReadsReturn's size (assuming it is at least '1').
            NamedPipeClientStreamStub pipeClient = new NamedPipeClientStreamStub {
                ReadsReturn = readsReturn
            };

            // Act
            XenConsoleStream xenConsoleStream = new XenConsoleStream(pipeClient);
            Thread.Sleep(100);

            // Assert
            Assert.AreEqual(1, pipeClient.ReadsCompleted);
            Assert.AreEqual(1, pipeClient.asyncReads.Count);
            foreach (KeyValuePair<string, int> read in pipeClient.asyncReads)
            {
                // This is incremented in 'EndRead()',
                // which is called in the callback.
                Assert.AreEqual(0, read.Value);
            }
            Assert.AreEqual(
                tmpBuffer,
                pipeClient.chunksRead[0]
            );
        }

        [Test]
        public void Constructor_MultipleAsyncReadsCompleted_Callback()
        {
            // Arrange
            byte[] tmpBuffer = new byte[NamedPipeClientStreamStub.BufferSize];

            string[] readsReturn = new string[] {
                RandomString.Generate(52),
                RandomString.Generate(77),
                RandomString.Generate(26),
            };

            int readsCompleted = 0;

            NamedPipeClientStreamStub pipeClient = new NamedPipeClientStreamStub
            {
                ReadsReturn = readsReturn,
                InvokeCallback = true
            };

            // Act
            XenConsoleStream xenConsoleStream = new XenConsoleStream(pipeClient);
            Thread.Sleep(200);

            // Assert
            for (int i = 0; i < pipeClient.chunksRead.Count; ++i)
            {
                NamedPipeClientStreamStub.UTF8Enc.GetBytes(
                    readsReturn[i],
                    0,
                    readsReturn[i].Length,
                    tmpBuffer,
                    0
                );

                Assert.AreEqual(tmpBuffer, pipeClient.chunksRead[i]);
            }

            foreach (KeyValuePair<string, int> read in pipeClient.asyncReads)
            {
                Assert.LessOrEqual(read.Value, 1);
                readsCompleted += read.Value;
            }

            // +1 because the last 'BeginRead()' does not complete
            Assert.AreEqual(readsReturn.Length + 1, pipeClient.asyncReads.Count);
            Assert.AreEqual(readsReturn.Length, readsCompleted);
            Assert.AreEqual(readsReturn.Length, pipeClient.ReadsCompleted);
        }

        [Test]
        public void OnXenConsoleMessageReceived_MessageLargerThanBuffer_NotSupportedExceptionThrown()
        {
            // Arrange
            byte[] tmpBuffer = new byte[NamedPipeClientStreamStub.BufferSize];

            string[] readsReturn = new string[] {
                RandomString.Generate(
                    2 * NamedPipeClientStreamStub.BufferSize
                )
            };

            NamedPipeClientStreamStub pipeClient = new NamedPipeClientStreamStub {
                ReadsReturn = readsReturn,
                InvokeCallback = true
            };

            // Act
            XenConsoleStream xenConsoleStream = new XenConsoleStream(pipeClient);

            // Assert
            Thread.Sleep(100);
            Assert.That(
                pipeClient.CallbackException,
                Is.InstanceOf<NotSupportedException>()
                    .And.With.Property("Message").EqualTo(String.Format(
                        "Message is larger than {0} bytes.",
                        NamedPipeClientStreamStub.BufferSize
                    ))
            );
        }
    }
}
