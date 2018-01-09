using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Management;

namespace xenguestlib
{
    namespace MibUtil
    {
       
        /// <summary>
        /// struct to unmanaged MIB_IF_ROW2 structure
        /// detailed, refer to MSDN(https://msdn.microsoft.com/en-us/library/windows/desktop/aa814491(v=vs.85).aspx)
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_IF_ROW2
        {
            public long InterfaceLuid;
            public int InterfaceIndex;
            public byte[] GUID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0)] // 514
            public string Alias;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0)] // 514
            public string Description;
            public int PhysicalAddressLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] PhysicalAddress;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] PermanentPhysicalAddress;
        }

        /// <summary>
        /// struct to unmanaged MIB_IF_TABLE2 structure
        /// detailed, refer to MSDN(https://msdn.microsoft.com/en-us/library/windows/hardware/ff559224(v=vs.85).aspx)
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_IF_TABLE2
        {
            public int NumEntries;
            public MIB_IF_ROW2[] Table;
        }

        /// <summary>
        /// class manage the MIB_IF_TABLE2 struct
        /// </summary>
        public class MibIFSingleton
        {

            [DllImportAttribute("Iphlpapi.dll")]
            static extern int GetIfTable2(ref int Table);

            [DllImportAttribute("Iphlpapi.dll")]
            static extern int FreeMibTable(int Table);
            /// <summary>
            /// Single instance
            /// </summary>
            private static MibIFSingleton instance;
            /// <summary>
            /// Get single instance
            /// </summary>
            private MibIFSingleton()
            {
                mibTable2 = new MIB_IF_TABLE2();
                bDirty = true;
            }

            private static MIB_IF_TABLE2 mibTable2;
            private static bool bDirty;
            private static Object updateLock = new Object();

            const int MIB_TABLE_SIZE = 1352;

            /// <summary>
            /// Singleton perperty to get the single instance
            /// </summary>
            public static MibIFSingleton Instance
            {
                get
                {
                    if(null == instance)
                    {
                        instance = new MibIFSingleton();
                    }
                    return instance;
                }
            }
            /// <summary>
            /// Get the managed MIB_IF_TABLE2 object, retrive if cache is dirty
            /// </summary>
            /// <returns></returns>
            public MIB_IF_TABLE2 getMIB2Interface()
            {
                if (true == bDirty) 
                {
                    makeMibTable();
                }
                return mibTable2;
            }

            /// <summary>
            /// Set cache is dirty, should be called(hooked) when the interface changed
            /// </summary>
            public void setDirty()
            {
                bDirty = true;
            }

            /// <summary>
            /// Construct the MIB_IF_TABLE2 object
            /// </summary>
            private void makeMibTable()
            {
                lock (updateLock)
                {
                    int pTable = 0;
                    int ret = GetIfTable2(ref pTable);
                    if (ret != 0)
                    {
                        string message = string.Format("GetIfTable2 got error: {0}", ret);
                        throw new Exception(message);
                    }
                    Debug.Print("Loading MIBtable2.....");
                    const int NUM_ENTRIES_LENGTH = 4;
                    mibTable2.Table = new MIB_IF_ROW2[mibTable2.NumEntries];
                    const int TABLE_POINTER_SIZE = 4;
                    int Address = pTable + NUM_ENTRIES_LENGTH + TABLE_POINTER_SIZE;
                    for (int i = 0; i < mibTable2.NumEntries; i++)
                    {
                        int offset = 0;
                        const int SINGLE_SIZE = 1;
                        const int MAX_SIZE = 1024;
                        long[] longBuf = new long[MAX_SIZE];
                        byte[] byteBuf = new byte[MAX_SIZE];
                        int[] intBuf = new int[MAX_SIZE];

                        int startAddr =  Address + (i) * MIB_TABLE_SIZE;
                        const int UUID_SIZE = 8;
                        // Set uuid
                        Marshal.Copy((IntPtr)startAddr, longBuf, 0, SINGLE_SIZE);
                        mibTable2.Table[i].InterfaceLuid = longBuf[0];
                        offset += UUID_SIZE;

                        // Set index
                        const int INDEX_SIZE = 4;
                        Marshal.Copy((IntPtr)(startAddr+offset), intBuf, 0, SINGLE_SIZE);
                        mibTable2.Table[i].InterfaceIndex = intBuf[0];
                        offset += INDEX_SIZE;

                        // Set guid
                        const int GUID_SIZE = 16;
                        mibTable2.Table[i].GUID = new byte[GUID_SIZE];
                        Marshal.Copy((IntPtr)(startAddr + offset), mibTable2.Table[i].GUID, 0, GUID_SIZE);
                        offset += GUID_SIZE;

                        // Set alias
                        const int ALIAS_SIZE = 514;
                        Marshal.Copy((IntPtr)(startAddr + offset), byteBuf, 0, GUID_SIZE);
                        mibTable2.Table[i].Alias = Encoding.Unicode.GetString(byteBuf);
                        offset += ALIAS_SIZE;

                        // Set desc
                        const int DESC_SIZE = 514;
                        Marshal.Copy((IntPtr)(startAddr + offset), byteBuf, 0, DESC_SIZE);
                        offset += DESC_SIZE;
                        mibTable2.Table[i].Description = Encoding.Unicode.GetString(byteBuf);

                        // Set physical address legnth
                        const int PHYSICAL_ADDR_LENGTH_SIZE = 4;
                        Marshal.Copy((IntPtr)(startAddr + offset), intBuf, 0, SINGLE_SIZE);
                        mibTable2.Table[i].PhysicalAddressLength = intBuf[0];
                        offset += PHYSICAL_ADDR_LENGTH_SIZE;

                        // Set physical address
                        const int PHYSICAL_ADDR_SIZE = 32;
                        mibTable2.Table[i].PhysicalAddress = new byte[PHYSICAL_ADDR_SIZE];
                        Marshal.Copy((IntPtr)(startAddr + offset), mibTable2.Table[i].PhysicalAddress, 0, PHYSICAL_ADDR_SIZE);

                        offset += PHYSICAL_ADDR_SIZE;

                        // Set permanent physical address
                        mibTable2.Table[i].PermanentPhysicalAddress = new byte[PHYSICAL_ADDR_SIZE];
                        Marshal.Copy((IntPtr)(startAddr + offset), mibTable2.Table[i].PermanentPhysicalAddress, 0, PHYSICAL_ADDR_SIZE);
                    }
                    FreeMibTable(pTable);
                    bDirty = false;
                }
            }
        
        }
    }

       
}
