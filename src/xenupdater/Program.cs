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
using System.Linq;
using System.Text;
using System.Security.Principal;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Threading;

namespace XenUpdater
{
    class Program
    {
        enum HRESULT : int
        {
            E_ACCESSDENIED = unchecked((int)0x80070005),
            E_INVALIDARG = unchecked((int)0x80070057)
        }
        static int Main(string[] args)
        {
            int result = -2;
            if (SingleApp.IsRunning())
                return -3;

            try
            {
                bool add = false;
                bool remove = false;
                bool check = true;

                if (!IsElevated())
                {
                    System.Diagnostics.Debug.Print("XenUpdater.exe must be run by an elevated administrator account");
                    return (int)HRESULT.E_ACCESSDENIED;
                }

                // check params for config options...
                foreach (string arg in args)
                {
                    switch (arg.ToLower())
                    {
                        case "add":
                            add = true;
                            check = false;
                            break;
                        case "remove":
                            remove = true;
                            check = false;
                            break;
                        case "check":
                            check = true;
                            break;
                        default:
                            throw new ArgumentException(arg);
                    }
                }
                if (add && !remove && !check)
                {
                    using (Tasks tasks = new Tasks())
                    {
                        tasks.AddTask();
                    }
                }
                if (remove && !add && !check)
                {
                    using (Tasks tasks = new Tasks())
                    {
                        tasks.RemoveTask();
                    }
                }
                if (check && !add && !remove)
                {
                    AutoUpdate auto = new AutoUpdate();
                    auto.CheckNow();
                }
                result = 0;
            }
            catch (UnauthorizedAccessException e)
            {
                System.Diagnostics.Debug.Print("Exception: " + e.ToString());
                result = (int)HRESULT.E_ACCESSDENIED;
            }
            catch (FormatException e)
            {
                System.Diagnostics.Debug.Print("Exception: " + e.ToString());
                result = (int)HRESULT.E_INVALIDARG;
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.Print("Exception: " + e.ToString());
                result = -1; // TODO: Return the HRESULT of this exception
            }

            SingleApp.Close();
            return result;
        }

        static bool IsElevated()
        {
            return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        }

        internal static class SingleApp
        {
            internal static bool IsRunning()
            {
                string guid = ((GuidAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(GuidAttribute), false).GetValue(0)).Value.ToString();
                var semaphoreName = @"Global\" + guid;
                try
                {
                    __semaphore = Semaphore.OpenExisting(semaphoreName, SemaphoreRights.Synchronize);

                    Close();
                    return true;
                }
                catch
                {
                    __semaphore = new Semaphore(0, 1, semaphoreName);
                    return false;
                }
            }

            internal static void Close()
            {
                if (__semaphore != null)
                {
                    __semaphore.Close();
                    __semaphore = null;
                }
            }

            private static Semaphore __semaphore;
        }
    }
}
