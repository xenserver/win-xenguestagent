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
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace XenGuestLib
{
    /// <summary>
    /// Utility class for accessing information about XenApp/XenDesktop
    /// </summary>
    public static class XenAppXenDesktop
    {
        // ReSharper disable InconsistentNaming
        // ReSharper disable UnusedMember.Local

        /// <summary>
        /// The classes of information that can be requested from WFQuerySessionInformation
        /// </summary>
        private enum WF_INFO_CLASS
        {
            WFVersion, // OSVERSIONINFO
            WFInitialProgram,
            WFWorkingDirectory,
            WFOEMId,
            WFSessionId,
            WFUserName,
            WFWinStationName,
            WFDomainName,
            WFConnectState,
            WFClientBuildNumber,
            WFClientName,
            WFClientDirectory,
            WFClientProductId,
            WFClientHardwareId,
            WFClientAddress,
            WFClientDisplay,
            WFClientCache,
            WFClientDrives,
            WFICABufferLength,
            WFLicenseEnabler,
            RESERVED2,
            WFApplicationName,
            WFVersionEx,
            WFClientInfo,
            WFUserInfo,
            WFAppInfo,
            WFClientLatency,
            WFSessionTime,
            WFLicensingModel
        }

        /// <summary>
        /// The states that the WinStation can be in, only really interested in WFConnected
        /// </summary>
        private enum WF_CONNECTSTATE_CLASS
        {
            WFActive,              // User logged on to WinStation
            WFConnected,           // WinStation connected to client
            WFConnectQuery,        // In the process of connecting to client
            WFShadow,              // Shadowing another WinStation
            WFDisconnected,        // WinStation logged on without client
            WFIdle,                // Waiting for client to connect
            WFListen,              // WinStation is listening for connection
            WFReset,               // WinStation is being reset
            WFDown,                // WinStation is down due to error
            WFInit                 // WinStation in initialization
        }

        // ReSharper enable UnusedMember.Local

        /// <summary>
        /// Delegate to the WFAPI method WFQuerySessionInformation
        /// </summary>
        /// <param name="hServer">The server to query, usually IntPtr.Zero for current server</param>
        /// <param name="sessionId">The session Id, -1 for current, 1 for console.</param>
        /// <param name="wfInfoClass">The query to perform</param>
        /// <param name="ppBuffer">The buffer containing the results, allocated internally, use WFFreeMemory to release</param>
        /// <param name="pBytesReturned">The size of the buffer returned</param>
        /// <returns>True if successful</returns>
        private delegate bool WFQuerySessionInformation(IntPtr hServer, int sessionId, WF_INFO_CLASS wfInfoClass, out IntPtr ppBuffer, out uint pBytesReturned);

        /// <summary>
        /// Frees memory previously allocated by WFQuerySessionInformation
        /// </summary>
        /// <param name="memory">The buffer to free</param>
        private delegate void WFFreeMemory(IntPtr memory);

        // ReSharper enable InconsistentNaming

        /// <summary>
        /// Is the console session being used remotely?
        /// </summary>
        /// <returns>True if there is an active remote session to console</returns>
        public static bool ActiveConsoleSession(uint sessionId)
        {
            // Load the correct bitness of wfapi
            var pDll = NativeMethods.LoadLibrary(IntPtr.Size == 8 ? @"wfapi64.dll" : @"wfapi.dll");

            if (pDll.Equals(IntPtr.Zero))
            {
                // Assume inability to load wfapi.dll means we're not in a XenApp/XenDesktop environment
                return false;
            }

            try
            {
                var pAddressOfQuerySessionInformationFunction = NativeMethods.GetProcAddress(
                    pDll,
                    "WFQuerySessionInformationW");
                var pAddressOfFreeFunction = NativeMethods.GetProcAddress(
                    pDll,
                    "WFFreeMemory");

                if (pAddressOfQuerySessionInformationFunction.Equals(IntPtr.Zero)
                    || pAddressOfFreeFunction.Equals(IntPtr.Zero))
                {
                    Debug.Print("Failed to load proc addresses for WFAPI, Query {0}, free {1}", pAddressOfQuerySessionInformationFunction, pAddressOfFreeFunction);
                    // Assume inability to find WFQuerySessionInformation also means we're not in a XenApp/XenDesktop environment
                    return false;
                }

                var wfQuerySessionInformation =
                    (WFQuerySessionInformation)
                        Marshal.GetDelegateForFunctionPointer(
                            pAddressOfQuerySessionInformationFunction,
                            typeof (WFQuerySessionInformation));

                var buffer = IntPtr.Zero;

                try
                {
                    uint bytesReturned;
                    if (wfQuerySessionInformation(
                        IntPtr.Zero,
                        (int)sessionId,
                        WF_INFO_CLASS.WFConnectState,
                        out buffer,
                        out bytesReturned))
                    {
                        return (WF_CONNECTSTATE_CLASS) Marshal.ReadInt32(buffer) == WF_CONNECTSTATE_CLASS.WFConnected;
                    }
                }
                finally
                {
                    if (!buffer.Equals(IntPtr.Zero))
                    {
                        var wfFreeMemory = (WFFreeMemory) Marshal.GetDelegateForFunctionPointer(
                            pAddressOfFreeFunction,
                            typeof (WFFreeMemory));
                        wfFreeMemory(buffer);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Print("Exception while checking for active XenApp/XenDesktop session, {0}", e);
                // Assume any exception means we're not in XenApp/XenDesktop
                return false;
            }
            finally
            {
                NativeMethods.FreeLibrary(pDll);
            }

            return false;
        }

        private static class NativeMethods
        {
            [DllImport("kernel32.dll")]
            public static extern IntPtr LoadLibrary(string dllToLoad);

            [DllImport("kernel32.dll")]
            public static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

            [DllImport("kernel32.dll")]
            public static extern bool FreeLibrary(IntPtr hModule);
        }
    }
}
