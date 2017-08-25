using Microsoft.Win32;
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
        event EventHandler Disconnected;
        void Start();
        bool IsConnected { get; }
        Func<string, bool> MessageForwardingRule { get; set; }
    }

    public interface IXenConsoleEventListener
    {
        void AttachToXenConsoleStream(IXenConsoleStream xcStream);
        void DetachFromXenConsoleStream();
        void XenConsoleMessageEventHandler(object sender, EventArgs e);
        void XenConsoleDisconnectedEventHandler(object sender, EventArgs e);
    }

    public class XenConsoleStreamFactory
    {
        private static readonly Type _type;
        private static readonly string dllPathRegKey =
            @"SOFTWARE\Citrix\XenTools\XenConsoleComm";

        private XenConsoleStreamFactory() { }

        private static string FindDllPath()
        {
            using (RegistryKey rk = Registry.LocalMachine.OpenSubKey(dllPathRegKey))
            {
                if (rk == null)
                    throw new DllNotFoundException(String.Format(
                        "Registry key '{0}' does not exist; "
                        + "path to XenConsoleComm.dll unknown.",
                        dllPathRegKey
                    ));

                return (string)rk.GetValue("DllPath");
            }
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
