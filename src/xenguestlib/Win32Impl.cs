/* Copyright (c) Citrix Systems Inc.
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, 
 * with or without modification, are permitted provided 
 * that the following conditions are met:
 *
 * *   Redistributions of source code must retain the above 
 *     copyright notice, this list of conditions and the 
 *     following disclaimer.
 * *   Redistributions in binary form must reproduce the above 
 *     copyright notice, this list of conditions and the 
 *     following disclaimer in the documentation and/or other 
 *     materials provided with the distribution.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND 
 * CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, 
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF 
 * MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR 
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, 
 * BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR 
 * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, 
 * WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF 
 * SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using System.Management;
using System.Windows.Forms;
using System.IO.Pipes;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using XenGuestLib;

namespace xenwinsvc
{
    public class Win32Impl
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr GetModuleHandle(string moduleName);

        [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule,
        [MarshalAs(UnmanagedType.LPStr)]string procName);

        static public bool is64BitOS()
        {

            if (IntPtr.Size == 8)
                return true;
            return isWOW64();
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool GetComputerNameEx(COMPUTER_NAME_FORMAT NameType,
           [Out] StringBuilder lpBuffer, ref uint lpnSize);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static public extern bool SetComputerNameEx(COMPUTER_NAME_FORMAT NameType,
            string lpBuffer);
        public enum COMPUTER_NAME_FORMAT
        {
            ComputerNameNetBIOS,
            ComputerNameDnsHostname,
            ComputerNameDnsDomain,
            ComputerNameDnsFullyQualified,
            ComputerNamePhysicalNetBIOS,
            ComputerNamePhysicalDnsHostname,
            ComputerNamePhysicalDnsDomain,
            ComputerNamePhysicalDnsFullyQualified,
        }
        static public string GetComputerDnsHostname()
        {
            uint size=0;
            StringBuilder hostname = null;

            while (!GetComputerNameEx(COMPUTER_NAME_FORMAT.ComputerNamePhysicalDnsHostname, hostname, ref size))
            {
                int err = Marshal.GetLastWin32Error();
                if ((err == ERROR_INSUFFICIENT_BUFFER) || (err == ERROR_MORE_DATA))
                {
                    size += 1;
                    hostname = new StringBuilder((int)size);
                }
                else
                {
                    throw new Exception("Unable to get computer name : " + err.ToString());
                }
            }

            return hostname.ToString();
        }
        public const int MAX_COMPUTERNAME_LENGTH = 15;

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);
        static public bool isWOW64()
        {
            bool flags;
            IntPtr modhandle = GetModuleHandle("kernel32.dll");
            if (modhandle == IntPtr.Zero)
                return false;
            if (GetProcAddress(modhandle, "IsWow64Process") == IntPtr.Zero)
                return false;

            if (IsWow64Process(GetCurrentProcess(), out flags))
                return flags;
            return false;
        }

        private static Exception Error(string message, int code)
        {
            WmiBase.Singleton.SetError(message + " : " + code.ToString());
            return new Exception("Win32 Error: "+message + " : " + code.ToString());
        }

        private static Exception Error(string message)
        {
            return Error(message, Marshal.GetLastWin32Error());
        }

        [DllImport("kernel32.dll")]        public static extern void SetLastError(uint dwErrCode);

        private const Int32 ANYSIZE_ARRAY = 1;
        private const UInt32 TOKEN_QUERY = 0x0008;
        private const UInt32 TOKEN_ADJUST_PRIVILEGES = 0x0020;
        public const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";
        private const UInt32 SE_PRIVILEGE_ENABLED = 0x00000002;

        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint FILE_SHARE_DELETE = 0x00000004;

        private const uint FILE_ATTRIBUTE_READONLY = 0x00000001;
        private const uint FILE_ATTRIBUTE_HIDDEN = 0x00000002;
        private const uint FILE_ATTRIBUTE_SYSTEM = 0x00000004;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
        private const uint FILE_ATTRIBUTE_ARCHIVE = 0x00000020;
        private const uint FILE_ATTRIBUTE_DEVICE = 0x00000040;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
        private const uint FILE_ATTRIBUTE_TEMPORARY = 0x00000100;
        private const uint FILE_ATTRIBUTE_SPARSE_FILE = 0x00000200;
        private const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x00000400;
        private const uint FILE_ATTRIBUTE_COMPRESSED = 0x00000800;
        private const uint FILE_ATTRIBUTE_OFFLINE = 0x00001000;
        private const uint FILE_ATTRIBUTE_NOT_CONTENT_INDEXED = 0x00002000;
        private const uint FILE_ATTRIBUTE_ENCRYPTED = 0x00004000;

        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint GENERIC_EXECUTE = 0x20000000;
        private const uint GENERIC_ALL = 0x10000000;

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LookupPrivilegeValue(IntPtr Null1, string lpName,
            out LUID lpLuid);
        public const string SE_ASSIGNPRIMARYTOKEN_NAME = "SeAssignPrimaryTokenPrivilege";
        public const string SE_AUDIT_NAME = "SeAuditPrivilege";
        public const string SE_BACKUP_NAME = "SeBackupPrivilege";
        public const string SE_CHANGE_NOTIFY_NAME = "SeChangeNotifyPrivilege";
        public const string SE_CREATE_GLOBAL_NAME = "SeCreateGlobalPrivilege";
        public const string SE_CREATE_PAGEFILE_NAME = "SeCreatePagefilePrivilege";
        public const string SE_CREATE_PERMANENT_NAME = "SeCreatePermanentPrivilege";
        public const string SE_CREATE_SYMBOLIC_LINK_NAME = "SeCreateSymbolicLinkPrivilege";
        public const string SE_CREATE_TOKEN_NAME = "SeCreateTokenPrivilege";
        public const string SE_DEBUG_NAME = "SeDebugPrivilege";
        public const string SE_ENABLE_DELEGATION_NAME = "SeEnableDelegationPrivilege";
        public const string SE_IMPERSONATE_NAME = "SeImpersonatePrivilege";
        public const string SE_INC_BASE_PRIORITY_NAME = "SeIncreaseBasePriorityPrivilege";
        public const string SE_INCREASE_QUOTA_NAME = "SeIncreaseQuotaPrivilege";
        public const string SE_INC_WORKING_SET_NAME = "SeIncreaseWorkingSetPrivilege";
        public const string SE_LOAD_DRIVER_NAME = "SeLoadDriverPrivilege";
        public const string SE_LOCK_MEMORY_NAME = "SeLockMemoryPrivilege";
        public const string SE_MACHINE_ACCOUNT_NAME = "SeMachineAccountPrivilege";
        public const string SE_MANAGE_VOLUME_NAME = "SeManageVolumePrivilege";
        public const string SE_PROF_SINGLE_PROCESS_NAME = "SeProfileSingleProcessPrivilege";
        public const string SE_RELABEL_NAME = "SeRelabelPrivilege";
        public const string SE_REMOTE_SHUTDOWN_NAME = "SeRemoteShutdownPrivilege";
        public const string SE_RESTORE_NAME = "SeRestorePrivilege";
        public const string SE_SECURITY_NAME = "SeSecurityPrivilege";
        public const string SE_SYNC_AGENT_NAME = "SeSyncAgentPrivilege";
        public const string SE_SYSTEM_ENVIRONMENT_NAME = "SeSystemEnvironmentPrivilege";
        public const string SE_SYSTEM_PROFILE_NAME = "SeSystemProfilePrivilege";
        public const string SE_SYSTEMTIME_NAME = "SeSystemtimePrivilege";
        public const string SE_TAKE_OWNERSHIP_NAME = "SeTakeOwnershipPrivilege";
        public const string SE_TCB_NAME = "SeTcbPrivilege";
        public const string SE_TIME_ZONE_NAME = "SeTimeZonePrivilege";
        public const string SE_TRUSTED_CREDMAN_ACCESS_NAME = "SeTrustedCredManAccessPrivilege";
        public const string SE_UNDOCK_NAME = "SeUndockPrivilege";
        public const string SE_UNSOLICITED_INPUT_NAME = "SeUnsolicitedInputPrivilege";

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle,
            UInt32 DesiredAccess, out IntPtr TokenHandle);

        //Use these for DesiredAccess
        public const UInt32 STANDARD_RIGHTS_REQUIRED = 0x000F0000;
        public const UInt32 STANDARD_RIGHTS_READ = 0x00020000;
        public const UInt32 TOKEN_ASSIGN_PRIMARY = 0x0001;
        public const UInt32 TOKEN_DUPLICATE = 0x0002;
        public const UInt32 TOKEN_IMPERSONATE = 0x0004;
        public const UInt32 TOKEN_QUERY_SOURCE = 0x0010;
        public const UInt32 TOKEN_ADJUST_GROUPS = 0x0040;
        public const UInt32 TOKEN_ADJUST_DEFAULT = 0x0080;
        public const UInt32 TOKEN_ADJUST_SESSIONID = 0x0100;
        public const UInt32 TOKEN_READ = (STANDARD_RIGHTS_READ | TOKEN_QUERY);
        public const UInt32 TOKEN_ALL_ACCESS = (STANDARD_RIGHTS_REQUIRED | TOKEN_ASSIGN_PRIMARY |
            TOKEN_DUPLICATE | TOKEN_IMPERSONATE | TOKEN_QUERY | TOKEN_QUERY_SOURCE |
            TOKEN_ADJUST_PRIVILEGES | TOKEN_ADJUST_GROUPS | TOKEN_ADJUST_DEFAULT |
            TOKEN_ADJUST_SESSIONID);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        static extern private bool WTSQueryUserToken(UInt32 sessionId, out IntPtr Token);

        public static IntPtr QueryUserToken(UInt32 sessionId)
        {
            IntPtr token;
            if (!WTSQueryUserToken(sessionId, out token)) {
                Error("WTSQueryUserToken");
            }
            return token;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AdjustTokenPrivileges(IntPtr TokenHandle,
            [MarshalAs(UnmanagedType.Bool)]bool DisableAllPrivileges,
            ref TOKEN_PRIVILEGES NewState,
            UInt32 Zero,
            IntPtr Null1,
            IntPtr Null2);

        public const UInt32 SE_PRIVILEGE_ENABLED_BY_DEFAULT = 0x00000001;
        public const UInt32 SE_PRIVILEGE_REMOVED = 0x00000004;
        public const UInt32 SE_PRIVILEGE_USED_FOR_ACCESS = 0x80000000;

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID_AND_ATTRIBUTES
        {
            public LUID Luid;
            public UInt32 Attributes;
        }

        public struct TOKEN_PRIVILEGES
        {
            public UInt32 PrivilegeCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = ANYSIZE_ARRAY)]
            public LUID_AND_ATTRIBUTES[] Privileges;
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        public static void KillProcess(IntPtr hProcess, uint uExitCode)
        {
            if (!TerminateProcess(hProcess, uExitCode)) {
                throw Error("TerminateProcess");
            }
        }

        [DllImport("kernel32.dll")]
        public static extern uint WTSGetActiveConsoleSessionId();

        public enum SL_GENUINE_STATE
        {
            SL_GEN_STATE_IS_GENUINE = 0,
            SL_GEN_STATE_INVALID_LICENSE = 1,
            SL_GEN_STATE_TAMPERED = 2,
            SL_GEN_STATE_LAST = 3
        }

        [DllImport("slc.dll", EntryPoint = "SLGetWindowsInformationDWORD",
        CharSet = CharSet.None, ExactSpelling = false,
        SetLastError = false)]
        private static extern uint SLGetWindowsInformationDWORD([MarshalAs(UnmanagedType.LPWStr)]string ValueName, ref uint value);

        public static uint GetWindowsInformation(string ValueName)
        {
            uint value = 0;
            uint result = SLGetWindowsInformationDWORD(ValueName, ref value);
            if (result != 0)
            {
                throw Error("SLGetWindowsInformationDWORD", (int)result);
            }
            return value;
        }

        [DllImport("Slwga.dll", EntryPoint = "SLIsGenuineLocal",
        CharSet = CharSet.None, ExactSpelling = false,
        SetLastError = false)]
        private static extern uint SLIsGenuineLocal(ref Guid appID, [In, Out] ref SL_GENUINE_STATE genuineState, IntPtr alwaysnull);


        public static SL_GENUINE_STATE IsGenuineWindows()
        {

            Guid ApplicationID = new Guid("55c92734-d682-4d71-983e-d6ec3f16059f");
            SL_GENUINE_STATE genState = SL_GENUINE_STATE.SL_GEN_STATE_LAST;

            uint result = SLIsGenuineLocal(ref ApplicationID, ref genState, IntPtr.Zero);
            if (result == 0)
            {
                return genState;
            }

            throw Error("SLIsGenuineLocal", (int)result);
            
        }

        [Flags]
        public enum ExitFlags : int {
            EWX_LOGOFF =      0x00000000,
            EWX_POWEROFF =    0x00000008,
            EWX_REBOOT =      0x00000002,
            EWX_RESTARTAPPS = 0x00000040,
            EWX_SHUTDOWN =    0x00000001,
            EWX_FORCE =       0x00000004,
            EWX_FORCEIFHUNG = 0x00000010
        }
        
        [DllImport("user32.dll", ExactSpelling=true, SetLastError=true) ]
        internal static extern bool ExitWindowsEx( ExitFlags flg, int rea );

        [DllImport("advapi32.dll", CharSet=CharSet.Auto, SetLastError=true)]
        public static extern bool InitiateSystemShutdownEx(
            string lpMachineName,
            string lpMessage,
            uint dwTimeout,
            bool bForceAppsClosed,
            bool bRebootAfterShutdown,
            ShutdownReason dwReason);
        
        [Flags]
        public enum ShutdownReason : uint
        {
            // Microsoft major reasons.
            SHTDN_REASON_MAJOR_OTHER        = 0x00000000,
            SHTDN_REASON_MAJOR_NONE         = 0x00000000,
            SHTDN_REASON_MAJOR_HARDWARE         = 0x00010000,
            SHTDN_REASON_MAJOR_OPERATINGSYSTEM      = 0x00020000,
            SHTDN_REASON_MAJOR_SOFTWARE         = 0x00030000,
            SHTDN_REASON_MAJOR_APPLICATION      = 0x00040000,
            SHTDN_REASON_MAJOR_SYSTEM           = 0x00050000,
            SHTDN_REASON_MAJOR_POWER        = 0x00060000,
            SHTDN_REASON_MAJOR_LEGACY_API       = 0x00070000,
        
            // Microsoft minor reasons.
            SHTDN_REASON_MINOR_OTHER        = 0x00000000,
            SHTDN_REASON_MINOR_NONE         = 0x000000ff,
            SHTDN_REASON_MINOR_MAINTENANCE      = 0x00000001,
            SHTDN_REASON_MINOR_INSTALLATION     = 0x00000002,
            SHTDN_REASON_MINOR_UPGRADE          = 0x00000003,
            SHTDN_REASON_MINOR_RECONFIG         = 0x00000004,
            SHTDN_REASON_MINOR_HUNG         = 0x00000005,
            SHTDN_REASON_MINOR_UNSTABLE         = 0x00000006,
            SHTDN_REASON_MINOR_DISK         = 0x00000007,
            SHTDN_REASON_MINOR_PROCESSOR        = 0x00000008,
            SHTDN_REASON_MINOR_NETWORKCARD      = 0x00000000,
            SHTDN_REASON_MINOR_POWER_SUPPLY     = 0x0000000a,
            SHTDN_REASON_MINOR_CORDUNPLUGGED    = 0x0000000b,
            SHTDN_REASON_MINOR_ENVIRONMENT      = 0x0000000c,
            SHTDN_REASON_MINOR_HARDWARE_DRIVER      = 0x0000000d,
            SHTDN_REASON_MINOR_OTHERDRIVER      = 0x0000000e,
            SHTDN_REASON_MINOR_BLUESCREEN       = 0x0000000F,
            SHTDN_REASON_MINOR_SERVICEPACK      = 0x00000010,
            SHTDN_REASON_MINOR_HOTFIX           = 0x00000011,
            SHTDN_REASON_MINOR_SECURITYFIX      = 0x00000012,
            SHTDN_REASON_MINOR_SECURITY         = 0x00000013,
            SHTDN_REASON_MINOR_NETWORK_CONNECTIVITY = 0x00000014,
            SHTDN_REASON_MINOR_WMI          = 0x00000015,
            SHTDN_REASON_MINOR_SERVICEPACK_UNINSTALL = 0x00000016,
            SHTDN_REASON_MINOR_HOTFIX_UNINSTALL     = 0x00000017,
            SHTDN_REASON_MINOR_SECURITYFIX_UNINSTALL = 0x00000018,
            SHTDN_REASON_MINOR_MMC          = 0x00000019,
            SHTDN_REASON_MINOR_TERMSRV          = 0x00000020,
        
            // Flags that end up in the event log code.
            SHTDN_REASON_FLAG_USER_DEFINED      = 0x40000000,
            SHTDN_REASON_FLAG_PLANNED           = 0x80000000,
            SHTDN_REASON_UNKNOWN            = SHTDN_REASON_MINOR_NONE,
            SHTDN_REASON_LEGACY_API         = (SHTDN_REASON_MAJOR_LEGACY_API | SHTDN_REASON_FLAG_PLANNED),
        
            // This mask cuts out UI flags.
            SHTDN_REASON_VALID_BIT_MASK         = 0xc0ffffff
        }
        
        public static void Reboot() {
            AcquireSystemPrivilege(SE_SHUTDOWN_NAME);
            WinVersion vers = new WinVersion();

            bool res = InitiateSystemShutdownEx("","", 0, true, true, 
                ShutdownReason.SHTDN_REASON_MAJOR_OTHER |
                ShutdownReason.SHTDN_REASON_MINOR_ENVIRONMENT |
                ShutdownReason.SHTDN_REASON_FLAG_PLANNED);

        }
        public static void Shutdown() {
            AcquireSystemPrivilege(SE_SHUTDOWN_NAME);
            WinVersion vers = new WinVersion();

           bool res = InitiateSystemShutdownEx("","", 0, true, false, 
               ShutdownReason.SHTDN_REASON_MAJOR_OTHER |
               ShutdownReason.SHTDN_REASON_MINOR_ENVIRONMENT |
               ShutdownReason.SHTDN_REASON_FLAG_PLANNED);


        }


        public static void AcquireSystemPrivilege(string name)
        {
            TOKEN_PRIVILEGES tkp;
            IntPtr token;

            tkp.Privileges = new LUID_AND_ATTRIBUTES[1];
            LookupPrivilegeValue(IntPtr.Zero, name, out tkp.Privileges[0].Luid);
            tkp.PrivilegeCount = 1;
            tkp.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;
            if (!OpenProcessToken(Process.GetCurrentProcess().Handle,
                TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out token))
            {
                throw Error("OpenProcessToken");
            }
            if (!AdjustTokenPrivileges(token, false, ref tkp, 0, IntPtr.Zero,
                IntPtr.Zero))
            {
                throw Error("AdjustTokenPrivileges"); ;
            }
        }
        public const UInt32 ERROR_NO_TOKEN = 1008;
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool CreateProcessAsUser(
            IntPtr hToken,
            string lpApplicationName,
            string lpCommandLine,
            //ref SECURITY_ATTRIBUTES lpProcessAttributes,
            IntPtr Null1,
            //ref SECURITY_ATTRIBUTES lpThreadAttributes,
            IntPtr Null2,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            //string lpCurrentDirectory,
            IntPtr Null3,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        public static IntPtr CreateUserProcess(IntPtr consoletoken, string fullpath,  string cmdline, PipeStream stdout = null) {
            Win32Impl.STARTUPINFO si = new Win32Impl.STARTUPINFO();
            if (stdout != null)
            {
                si.hStdOutput = stdout.SafePipeHandle.DangerousGetHandle();
                si.dwFlags = (uint)Win32Impl.STARTF.STARTF_USESTDHANDLES;
            }
            Win32Impl.PROCESS_INFORMATION pi = new Win32Impl.PROCESS_INFORMATION();
            if (!Win32Impl.CreateProcessAsUser(consoletoken, fullpath, cmdline, IntPtr.Zero, IntPtr.Zero, false, 0, IntPtr.Zero, IntPtr.Zero, ref si, out pi))
            {
                throw Error("CreateProcessAsUser");
            }
            return pi.hProcess;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
        {
            public Int32 cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public Int32 dwX;
            public Int32 dwY;
            public Int32 dwXSize;
            public Int32 dwYSize;
            public Int32 dwXCountChars;
            public Int32 dwYCountChars;
            public Int32 dwFillAttribute;
            public UInt32 dwFlags;
            public Int16 wShowWindow;
            public Int16 cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }
        public enum  STARTF : uint
        {
            STARTF_USESHOWWINDOW    = 0x00000001,
            STARTF_USESIZE          = 0x00000002,
            STARTF_USEPOSITION      = 0x00000004,
            STARTF_USECOUNTCHARS    = 0x00000008,
            STARTF_USEFILLATTRIBUTE = 0x00000010,
            STARTF_RUNFULLSCREEN    = 0x00000020, 
            STARTF_FORCEONFEEDBACK  = 0x00000040,
            STARTF_FORCEOFFFEEDBACK = 0x00000080,
            STARTF_USESTDHANDLES    = 0x00000100,
        }
        struct OSVERSIONINFO
        {
            public uint dwOSVersionInfoSize;
            public uint dwMajorVersion;
            public uint dwMinorVersion;
            public uint dwBuildNumber;
            public uint dwPlatformId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szCSDVersion;
            public Int16 wServicePackMajor;
            public Int16 wServicePackMinor;
            public Int16 wSuiteMask;
            public Byte wProductType;
            public Byte wReserved;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEMTIME
        {
            [MarshalAs(UnmanagedType.U2)]
            public short Year;
            [MarshalAs(UnmanagedType.U2)]
            public short Month;
            [MarshalAs(UnmanagedType.U2)]
            public short DayOfWeek;
            [MarshalAs(UnmanagedType.U2)]
            public short Day;
            [MarshalAs(UnmanagedType.U2)]
            public short Hour;
            [MarshalAs(UnmanagedType.U2)]
            public short Minute;
            [MarshalAs(UnmanagedType.U2)]
            public short Second;
            [MarshalAs(UnmanagedType.U2)]
            public short Milliseconds;

            public SYSTEMTIME(DateTime dt)
            {
                dt = dt.ToUniversalTime();  // SetSystemTime expects the SYSTEMTIME in UTC
                Year = (short)dt.Year;
                Month = (short)dt.Month;
                DayOfWeek = (short)dt.DayOfWeek;
                Day = (short)dt.Day;
                Hour = (short)dt.Hour;
                Minute = (short)dt.Minute;
                Second = (short)dt.Second;
                Milliseconds = (short)dt.Millisecond;
            }
        }
        [DllImport("kernel32.dll")]
        public static extern bool SetSystemTime(ref SYSTEMTIME time);

        [DllImport("kernel32.dll")]
        private static extern IntPtr FindFirstVolume([Out] StringBuilder lpszVolumeName,
           uint cchBufferLength);
        [DllImport("kernel32.dll")]
        private static extern bool FindNextVolume(IntPtr hFindVolume, [Out] StringBuilder
           lpszVolumeName, uint cchBufferLength);
        [DllImport("kernel32.dll")]
        private static extern bool FindVolumeClose(IntPtr hFindVolume);
        const int INVALID_HANDLE_VALUE = -1;
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetDiskFreeSpaceEx(string lpDirectoryName,
        out ulong lpFreeBytesAvailable,
        out ulong lpTotalNumberOfBytes,
        out ulong lpTotalNumberOfFreeBytes);
        [Flags]
        public enum EFileAccess : uint
        {
            GenericRead = 0x80000000,
            GenericWrite = 0x40000000,
            GenericExecute = 0x20000000,
            GenericAll = 0x10000000,
            Any = 0
        }

        [Flags]
        public enum EFileShare : uint
        {
            None = 0x00000000,
            Read = 0x00000001,
            Write = 0x00000002,
            Delete = 0x00000004
        }

        public enum ECreationDisposition : uint
        {

            New = 1,
            CreateAlways = 2,
            OpenExisting = 3,
            OpenAlways = 4,
            TruncateExisting = 5
        }

        [Flags]
        public enum EFileAttributes : uint
        {
            Readonly = 0x00000001,
            Hidden = 0x00000002,
            System = 0x00000004,
            Directory = 0x00000010,
            Archive = 0x00000020,
            Device = 0x00000040,
            Normal = 0x00000080,
            Temporary = 0x00000100,
            SparseFile = 0x00000200,
            ReparsePoint = 0x00000400,
            Compressed = 0x00000800,
            Offline = 0x00001000,
            NotContentIndexed = 0x00002000,
            Encrypted = 0x00004000,
            Write_Through = 0x80000000,
            Overlapped = 0x40000000,
            NoBuffering = 0x20000000,
            RandomAccess = 0x10000000,
            SequentialScan = 0x08000000,
            DeleteOnClose = 0x04000000,
            BackupSemantics = 0x02000000,
            PosixSemantics = 0x01000000,
            OpenReparsePoint = 0x00200000,
            OpenNoRecall = 0x00100000,
            FirstPipeInstance = 0x00080000
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetVolumeNameForVolumeMountPoint(string
           lpszVolumeMountPoint, [Out] StringBuilder lpszVolumeName,
           uint cchBufferLength);

        public static string GetVolumeNameFromMountPoint(string MountPoint)
        {
            const int MaxVolumeNameLength = 50;
            StringBuilder sb = new StringBuilder(MaxVolumeNameLength);
            if (!GetVolumeNameForVolumeMountPoint(MountPoint, sb, (uint)MaxVolumeNameLength))
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            return sb.ToString();
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateFile(
            [MarshalAs(UnmanagedType.LPTStr)]string lpFileName,
            [MarshalAs(UnmanagedType.U4)]EFileAccess dwDesiredAccess,
            [MarshalAs(UnmanagedType.U4)]EFileShare dwShareMode,
            [MarshalAs(UnmanagedType.SysInt)]IntPtr lpSecurityAttributes,
            [MarshalAs(UnmanagedType.U4)]ECreationDisposition dwCreationDisposition,
            [MarshalAs(UnmanagedType.U4)]EFileAttributes dwFlagsAndAttributes,
            [MarshalAs(UnmanagedType.U4)]uint hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "CloseHandle")]
        private static extern uint CloseHandle(IntPtr handle);

        public static void Close(IntPtr handle)
        {
            if (CloseHandle(handle)==0)
            {
                throw Error("CloseHandle");
            }
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool DeviceIoControl(
            [MarshalAs(UnmanagedType.SysInt)]IntPtr hDevice,
            [MarshalAs(UnmanagedType.U4)]IoctlControlCodes dwIoControlCode,
            IntPtr lpInBuffer,
            [MarshalAs(UnmanagedType.U4)]int nInBufferSize,
            IntPtr lpOutBuffer,
            [MarshalAs(UnmanagedType.U4)]int nOutBufferSize,
            [MarshalAs(UnmanagedType.U4)]out int lpBytesReturned,
            [MarshalAs(UnmanagedType.U4)]uint lpOverlapped);

        private enum IoctlMethod : uint
        {
            Buffered = 0,
            InDirect = 1,
            OutDirect = 2,
            Neither = 3
        }
        private enum IoctlAccess : uint
        {
            Any = 0,
            Read = 1,
            Write = 2
        }

        private enum IoctlBase : uint
        {
            IOCTL_VOLUME_BASE = 0x56 //'V'
        }

        private enum IoctlControlCodes : uint
        {
            IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS = (IoctlBase.IOCTL_VOLUME_BASE << 16) | (IoctlAccess.Any << 14) | (0 << 2) | (IoctlMethod.Buffered)
        }
        [StructLayout(LayoutKind.Sequential)]
        public class VOLUME_DISK_EXTENTS
        {
            public uint NumberOfDiskExtents;
            public uint align;
        }
        [StructLayout(LayoutKind.Sequential)]
        public class DISK_EXTENT
        {
            public uint DiskNumber;
            public UInt64 StartingOffset;
            public UInt64 ExtentLength;
        }
        public class Extent
        {
            public uint DiskNumber;
            public UInt64 StartingOffset;
            public UInt64 ExtentLength;
        }
        const int ERROR_MORE_DATA = 0xea;
        const int ERROR_INSUFFICIENT_BUFFER = 0x7a;

        public class Extents : List<Extent>
        {

            public Extents(string name)
            {

                // Remove trailing '/' character from path
                string newname = name.Substring(0, name.Length - 1);

                IntPtr volhandle = CreateFile(newname, EFileAccess.Any, EFileShare.Read | EFileShare.Write, IntPtr.Zero, ECreationDisposition.OpenExisting, EFileAttributes.Normal, 0);
                if (volhandle.ToInt32() == INVALID_HANDLE_VALUE)
                {
                    return;
                }

                try
                {
                    VOLUME_DISK_EXTENTS vextents = new VOLUME_DISK_EXTENTS();
                    DISK_EXTENT dextent = new DISK_EXTENT();
                    uint numextents = 1;
                    int allocsize = (int)Marshal.SizeOf(vextents) + (int)numextents * (int)Marshal.SizeOf(dextent);
                    IntPtr memblock = IntPtr.Zero;
                    IntPtr alloced;
                    memblock = Marshal.AllocHGlobal((int)allocsize);
                    alloced = memblock;
                    try {
                        int returned = 0;

                        VOLUME_DISK_EXTENTS diskExtents = new VOLUME_DISK_EXTENTS();
                        while (true)
                        {
                            if (!DeviceIoControl(volhandle, IoctlControlCodes.IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS, IntPtr.Zero, 0, memblock, allocsize, out returned, 0))
                            {
                                int err = Marshal.GetLastWin32Error();
                                if ((err == ERROR_INSUFFICIENT_BUFFER) || (err == ERROR_MORE_DATA))
                                {
                                    Marshal.PtrToStructure(memblock, diskExtents);
                                    numextents = diskExtents.NumberOfDiskExtents;
                                    allocsize = (int)Marshal.SizeOf(vextents) + (int)numextents * (int)Marshal.SizeOf(dextent);
                                    memblock = Marshal.ReAllocHGlobal(memblock, new IntPtr(allocsize));
                                    alloced = memblock;
                                    continue;
                                }
                                else
                                {
                                    return;
                                }
                            }
                            break;
                        }
                        Marshal.PtrToStructure(memblock, diskExtents);
                        memblock = new IntPtr(memblock.ToInt64() + Marshal.SizeOf(diskExtents));
                        for (int i = 0; i < diskExtents.NumberOfDiskExtents; i++)
                        {
                            Marshal.PtrToStructure(memblock, dextent);
                            Extent extent = new Extent { DiskNumber = dextent.DiskNumber, ExtentLength = dextent.ExtentLength, StartingOffset = dextent.StartingOffset };
                            this.Add(extent);
                            memblock = new IntPtr(memblock.ToInt64() + Marshal.SizeOf(dextent));
                        }
                    }
                    finally {
                        Marshal.FreeHGlobal(alloced);
                    }

                }
                catch (Exception e)
                {
                    WmiBase.Singleton.DebugMsg("Extent listing exception : "+ e.ToString());
                    throw;
                }
                finally
                {
                    CloseHandle(volhandle);
                }
            }
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetVolumePathNamesForVolumeNameW(
                [MarshalAs(UnmanagedType.LPWStr)]
            string lpszVolumeName,
                [MarshalAs(UnmanagedType.LPWStr)]
            string lpszVolumePathNames,
                uint cchBuferLength,
                ref UInt32 lpcchReturnLength);
        const int MAX_PATH = 260;
        [DllImport("Kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private extern static bool GetVolumeInformation(
          string RootPathName,
          StringBuilder VolumeNameBuffer,
          int VolumeNameSize,
          out uint VolumeSerialNumber,
          out uint MaximumComponentLength,
          out uint FileSystemFlags,
          StringBuilder FileSystemNameBuffer,
          int nFileSystemNameSize);

        public class Volume
        {
            string intName;
            public string Name { get { return intName; } }
            ulong intSizeBytes;
            ulong intFreeBytes;
            public ulong SizeBytes { get { return intSizeBytes; } }
            public ulong FreeBytes { get { return intFreeBytes; } }
            public Extents extents;
            public string VolumeName;
            public string FSName;
            void getVolumeInformation()
            {
                StringBuilder volumename = new StringBuilder(MAX_PATH + 1);
                StringBuilder fsname = new StringBuilder(MAX_PATH + 1);
                uint serial;
                uint maxcomponent;
                uint flags;

                if (GetVolumeInformation(intName, volumename, volumename.Capacity, out serial, out maxcomponent, out flags, fsname, fsname.Capacity))
                {
                    VolumeName = volumename.ToString();
                    FSName = fsname.ToString();
                }
            }
            List<string> getPathNames()
            {
                uint returnlength = 0;
                string namebuffer = "";
                List<string> paths = new List<string>();
                while (!GetVolumePathNamesForVolumeNameW(intName, namebuffer, (uint)namebuffer.Length, ref returnlength))
                {
                    if (returnlength == 0)
                    {
                        return paths;
                    }
                    if (returnlength == namebuffer.Length)
                    {
                        returnlength += 128;
                    }

                    namebuffer = new string(new char[returnlength]);
                }
                string[] patharray = namebuffer.Split('\0');
                foreach (string path in patharray)
                {
                    if (path.Length > 0)
                    {
                        paths.Add(path);
                    }
                }
                return paths;


            }
            public Volume(string name)
            {
                intName = name;
                ulong tmp;
                GetDiskFreeSpaceEx(Name, out tmp, out intSizeBytes, out intFreeBytes);
                extents = new Extents(name);
                pathnames = getPathNames();
                getVolumeInformation();
            }
            public List<string> pathnames;
            public static int compare(Volume x, Volume y)
            {
                return x.Name.CompareTo(y.Name);
            }
        }



        public class Volumes : ICollection<Volume>
        {
            List<Volume> volumes;
            class PairComparison
            {
                IEnumerator<Volume> enumold;
                IEnumerator<Volume> enumnew;
                List<Volume> removed;
                List<Volume> added;
                List<Volume[]> changed;
                bool enumoldnext()
                {
                    if (!enumold.MoveNext())
                    {
                        while (enumnew.MoveNext())
                        {
                            added.Add(enumnew.Current);
                        }
                        return false;
                    }
                    return true;
                }
                bool enumnewnext()
                {
                    if (!enumnew.MoveNext())
                    {
                        while (enumold.MoveNext())
                        {
                            removed.Add(enumold.Current);
                        }
                        return false;
                    }
                    return true;
                }
                void compare()
                {
                    enumold.Reset();
                    enumnew.Reset();
                    if (!(enumnewnext() && enumoldnext())) return;
                    while (true)
                    {
                        int res = Volume.compare(enumold.Current, enumnew.Current);
                        if (res == 0)
                        {
                            changed.Add(new Volume[] { enumold.Current, enumnew.Current });
                            if (!(enumnewnext() && enumoldnext())) return;
                        }
                        if (res < 0)
                        {
                            removed.Add(enumold.Current);
                            if (!enumoldnext()) return;
                        }
                        if (res > 0)
                        {
                            added.Add(enumnew.Current);
                            if (!enumnewnext()) return;
                        }
                    }
                }
                public static void Compare(IEnumerator<Volume> enumold, IEnumerator<Volume> enumnew, out List<Volume> removed, out List<Volume> added, out List<Volume[]> changed)
                {
                    removed = new List<Volume>();
                    added = new List<Volume>();
                    changed = new List<Volume[]>();
                    PairComparison pc = new PairComparison() { enumnew = enumnew, enumold = enumold, removed = removed, added = added, changed = changed };
                    pc.compare();
                }
            }
            public static void CompareVolumes(Volumes oldvols, Volumes newvols, out List<Volume> removed, out List<Volume> added, out List<Volume[]> changed)
            {

                IEnumerator<Volume> enumold = ((IEnumerable<Volume>)oldvols).GetEnumerator();
                IEnumerator<Volume> enumnew = ((IEnumerable<Volume>)oldvols).GetEnumerator();
                PairComparison.Compare(enumold, enumnew, out removed, out added, out changed);
            }
            public Volumes()
            {
                volumes = new List<Volume>();
                const int size = 1024;
                StringBuilder volname = new StringBuilder(size, size);
                IntPtr ptr = FindFirstVolume(volname, size);
                if (ptr.ToInt32() != INVALID_HANDLE_VALUE)
                {
                    volumes.Add(new Volume(volname.ToString()));
                    while (FindNextVolume(ptr, volname, size))
                    {
                        volumes.Add(new Volume(volname.ToString()));
                    }
                    FindVolumeClose(ptr);
                    volumes.Sort(Volume.compare);
                }
                intCount = volumes.Count;


            }
            int intCount;
            public int Count { get { return intCount; } }
            public bool IsReadOnly { get { return true; } }
            public void Add(Volume item)
            {
                throw new System.NotSupportedException();
            }
            public void Clear()
            {
                throw new System.NotSupportedException();
            }
            public bool Contains(Volume item)
            {
                return volumes.Contains(item);
            }
            public void CopyTo(Volume[] array, int arrayIndex)
            {
                volumes.CopyTo(array, arrayIndex);
            }
            public bool Remove(Volume item)
            {
                throw new System.NotSupportedException();
            }
            System.Collections.Generic.IEnumerator<Volume> System.Collections.Generic.IEnumerable<Volume>.GetEnumerator()
            {
                return volumes.GetEnumerator();
            }
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return volumes.GetEnumerator();
            }


        }


        [DllImport("kernel32")]
        private static extern bool GetVersionEx(ref OSVERSIONINFO osvi);

        public class WinVersion
        {
            OSVERSIONINFO osvi;
            public enum ProductType :uint {
                NT_WORKSTATION =  1,
                NT_DOMAIN_CONTROLLER = 2,
                NT_SERVER = 3
            } 
            public WinVersion()
            {
                osvi = new OSVERSIONINFO();
                osvi.dwOSVersionInfoSize = (uint)Marshal.SizeOf(typeof(OSVERSIONINFO));

                GetVersionEx(ref osvi);
            }
            public uint GetPlatformId() { return osvi.dwPlatformId; }
            public uint GetServicePackMajor() { return (uint)osvi.wServicePackMajor; }
            public uint GetServicePackMinor() { return (uint)osvi.wServicePackMinor; }
            public uint GetSuite() { return (uint)osvi.wSuiteMask; }
            public uint GetProductType() { return osvi.wProductType; }
            public uint GetVersionValue() { 
                uint vers =  ((osvi.dwMajorVersion <<8) | osvi.dwMinorVersion);
                return vers; 
            }
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool LogonUser(
            string lpszUsername,
            string lpszDomain,
            string lpszPassword,
            int dwLogonType,
            int dwLogonProvider,
            out IntPtr phToken
            );
        public enum LogonType
        {
            /// <summary>
            /// This logon type is intended for users who will be interactively using the computer, such as a user being logged on  
            /// by a terminal server, remote shell, or similar process.
            /// This logon type has the additional expense of caching logon information for disconnected operations; 
            /// therefore, it is inappropriate for some client/server applications,
            /// such as a mail server.
            /// </summary>
            LOGON32_LOGON_INTERACTIVE = 2,

            /// <summary>
            /// This logon type is intended for high performance servers to authenticate plaintext passwords.

            /// The LogonUser function does not cache credentials for this logon type.
            /// </summary>
            LOGON32_LOGON_NETWORK = 3,

            /// <summary>
            /// This logon type is intended for batch servers, where processes may be executing on behalf of a user without 
            /// their direct intervention. This type is also for higher performance servers that process many plaintext
            /// authentication attempts at a time, such as mail or Web servers. 
            /// The LogonUser function does not cache credentials for this logon type.
            /// </summary>
            LOGON32_LOGON_BATCH = 4,

            /// <summary>
            /// Indicates a service-type logon. The account provided must have the service privilege enabled. 
            /// </summary>
            LOGON32_LOGON_SERVICE = 5,

            /// <summary>
            /// This logon type is for GINA DLLs that log on users who will be interactively using the computer. 
            /// This logon type can generate a unique audit record that shows when the workstation was unlocked. 
            /// </summary>
            LOGON32_LOGON_UNLOCK = 7,

            /// <summary>
            /// This logon type preserves the name and password in the authentication package, which allows the server to make 
            /// connections to other network servers while impersonating the client. A server can accept plaintext credentials 
            /// from a client, call LogonUser, verify that the user can access the system across the network, and still
            /// communicate with other servers.
            /// NOTE: Windows NT:  This value is not supported. 
            /// </summary>
            LOGON32_LOGON_NETWORK_CLEARTEXT = 8,

            /// <summary>
            /// This logon type allows the caller to clone its current token and specify new credentials for outbound connections.
            /// The new logon session has the same local identifier but uses different credentials for other network connections. 
            /// NOTE: This logon type is supported only by the LOGON32_PROVIDER_WINNT50 logon provider.
            /// NOTE: Windows NT:  This value is not supported. 
            /// </summary>
            LOGON32_LOGON_NEW_CREDENTIALS = 9,
        }

        public enum LogonProvider
        {
            /// <summary>
            /// Use the standard logon provider for the system. 
            /// The default security provider is negotiate, unless you pass NULL for the domain name and the user name 
            /// is not in UPN format. In this case, the default provider is NTLM. 
            /// NOTE: Windows 2000/NT:   The default security provider is NTLM.
            /// </summary>
            LOGON32_PROVIDER_DEFAULT = 0,
            LOGON32_PROVIDER_WINNT35 = 1,
            LOGON32_PROVIDER_WINNT40 = 2,
            LOGON32_PROVIDER_WINNT50 = 3
        }
        static public IntPtr LogOn(string user, string domain, string password) {
            IntPtr phToken;
            if (!Win32Impl.LogonUser(user, domain, password, (int)LogonType.LOGON32_LOGON_INTERACTIVE, (int)LogonProvider.LOGON32_PROVIDER_DEFAULT, out phToken))
            {
                throw new Exception("Logon Failed " + Marshal.GetLastWin32Error().ToString());
            }
            return phToken;
         }


    }

    class ProcessWaitHandle : WaitHandle
    {
        public ProcessWaitHandle(SafeWaitHandle process)
        {
            this.SafeWaitHandle = process;
        }
    }

    

}
