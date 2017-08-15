using System;
using System.Reflection;

namespace IXenConsoleComm
{
    public interface IXenConsoleMessageEventArgs
    {
        string Value { get; }
        void Reply(string value);
    }

    public interface IXenConsoleStream
    {
        event EventHandler MessageReceived;
        event EventHandler PipeDisconnected;
        Func<string, bool> MessageForwardingRule { get; set; }
    }

    public interface IXenConsoleEventListener
    {
        void AttachToXenConsoleStream(IXenConsoleStream xcStream);
        void DetachFromXenConsoleStream();
        void XenConsoleMessageEventHandler(object sender, EventArgs e);
    }

    public class XenConsoleStreamFactory
    {
        private static readonly Type _type;

        private XenConsoleStreamFactory() { }

        private static string FindDllPath()
        {
            // TODO: Search for the file
            return
                @"C:\Users\Administrator\workspace\win-xenguestagent\proj\XenConsoleComm\bin\Debug\XenConsoleComm.dll";
        }

        private static void ThrowIfVersionsIncompatible(string dllPath)
        {
            Version interfaceVersion = Assembly.GetExecutingAssembly().GetName().Version;
            Version implementationVersion = AssemblyName.GetAssemblyName(dllPath).Version;

            if (interfaceVersion.Major != implementationVersion.Major
                || (interfaceVersion.Major == implementationVersion.Major
                    && interfaceVersion.Minor != implementationVersion.Minor))
            {
                throw new ApplicationException(String.Format(
                    "IXenConsoleComm version ({0}) incompatible with XenConsoleComm version ({1}).",
                    interfaceVersion,
                    implementationVersion
                ));
            }
        }

        // If this throws an exception once, it will always
        // throw exceptions until App is restarted
        static XenConsoleStreamFactory()
        {
            string dllPath = FindDllPath();
            ThrowIfVersionsIncompatible(dllPath);

            _type = Assembly
                .LoadFrom(dllPath)
                .GetType("XenConsoleComm.XenConsoleStream");
        }

        public static IXenConsoleStream Create()
        {
            return (IXenConsoleStream)Activator.CreateInstance(_type);
        }
    }
}
