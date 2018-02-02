using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;

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
                    const int SINGLE_SIZE = 1;
                    const int MAX_SIZE = 1024;
                    long[] longBuf = new long[MAX_SIZE];
                    byte[] byteBuf = new byte[MAX_SIZE];
                    int[] intBuf = new int[MAX_SIZE];

                    // Get number of entries
                    Marshal.Copy((IntPtr)pTable, intBuf, 0, NUM_ENTRIES_LENGTH);
                    mibTable2.NumEntries = intBuf[0];

                    mibTable2.Table = new MIB_IF_ROW2[mibTable2.NumEntries];
                    const int TABLE_POINTER_SIZE = 4;
                    int Address = pTable + NUM_ENTRIES_LENGTH + TABLE_POINTER_SIZE;
                    for (int i = 0; i < mibTable2.NumEntries; i++)
                    {
                        int offset = 0;

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
    namespace NicUtil 
    {
        using MibUtil;
        using System.Net.NetworkInformation;
        public class NicUtil 
        {
            /// <summary>
            /// whether a nic macth with the providen mac address
            /// </summary>
            /// <param name="mac">mac address to be match</param>
            /// <param name="nic">the nic object representing a NIC inside VM</param>
            /// <returns></returns>
            public static bool macsMatch(string mac, NetworkInterface nic)
            {
                byte[] softMacbytes = nic.GetPhysicalAddress().GetAddressBytes();
                byte[] permanentMacBytes = getPermanentMacFromSoftMac(softMacbytes);
                if (null == permanentMacBytes)
                {
                    Debug.Print("Could not find permanent mac address for {0}, use soft mac", nic.GetPhysicalAddress().ToString());
                    permanentMacBytes = softMacbytes;
                }
                byte[] macBytes = getByteArrayfromMacString(mac);
                return matchByteArray(macBytes,permanentMacBytes);
            }
            /// <summary>
            /// Whether a permanent mac  string can match an soft mac string, the soft mac string is used to find the permanent mac
            /// </summary>
            /// <param name="permanentMacStr">formated mac string representing the permanent mac addr</param>
            /// <param name="softMacString">soft mac string</param>
            /// <returns>true if match, otherwise false</returns>
            public static bool macsMatch(string permanentMacStr, string softMacString) 
            {
                if (null == permanentMacStr || null == softMacString) return false;
                byte[] permanentMacBytes = getPermanentMacFromSoftMac(softMacString);
                byte[] macBytes = getByteArrayfromMacString(permanentMacStr);
                return matchByteArray(permanentMacBytes, macBytes);
            }

            /// <summary>
            /// Get permanent mac address from soft mac string
            /// </summary>
            /// <param name="softMacString">soft mac string to be searched</param>
            /// <returns>the permanent mac address bytes according to soft mac, null if not found</returns>
            public static byte[] getPermanentMacFromSoftMac(string softMacString)
            {
                byte[] softMacBytes = getByteArrayfromMacString(softMacString);
                return getPermanentMacFromSoftMac(softMacBytes);
            } 
           
            /// <summary>
            /// Get permanent mac address from soft mac bytes
            /// </summary>
            /// <param name="softMacBytes">soft mac byte arrays to be searched</param>
            /// <returns>the permanent mac address bytes according to soft mac, null if not found</returns>
            public static byte[] getPermanentMacFromSoftMac(byte[] softMacBytes)
            {
                MIB_IF_TABLE2 mibTable = MibIFSingleton.Instance.getMIB2Interface();
                MIB_IF_ROW2[] mibRows = (from mibIfRow in mibTable.Table where matchByteArray(softMacBytes, mibIfRow.PhysicalAddress) select mibIfRow).ToArray<MIB_IF_ROW2>();
                try
                {
                    if (null == mibRows || 0 == mibRows.Length)
                    {
                        return null; // Does not find the permanent mac
                    }
                    else
                    {
                        byte[] matchBytes = mibRows[0].PermanentPhysicalAddress;
                        if (mibRows[0].PhysicalAddressLength != 6)
                        {
                            Debug.Print("found non-ethernet physical address, taken as not found");
                            return null;
                        }
                        return matchBytes;
                    }
                }
                catch (Exception e)
                {
                    Debug.Print("get exception: {0}", e);
                    return null;
                }
            }

            /// <summary>
            /// Get the mac address string from the byte array 
            /// </summary>
            /// <param name="macArr">mac address byte array, should at least 6 bytes</param>
            /// <returns>mac address string</returns>
            public static string getMacStringFromByteArray(byte[] macArr)
            {
                if (null == macArr || 6 > macArr.Length)
                {
                    throw new Exception("Invalid mac address format");
                }
                string macString = string.Format("{0:X2}:{1:X2}:{2:X2}:{3:X2}:{4:X2}:{5:X2}", macArr[0], macArr[1], macArr[2], macArr[3], macArr[4], macArr[5]);
                return macString.ToLower();
            }

            /// <summary>
            ///  Compare two byte array.
            ///     compare stop length is reached, or end is reached
            /// </summary>
            /// <param name="arr1">first byte array </param>
            /// <param name="arr2">second byte array</param>
            /// <param name="length">length want to compare</param>
            /// <returns></returns>
            public static bool matchByteArray(byte[] arr1, byte[] arr2, int length = 6)
            {
                if (null == arr1 || null == arr2) return false;
                formatArray(ref arr1, length);
                formatArray(ref arr2, length);
                return arr1.SequenceEqual(arr2);
            }

            /// <summary>
            /// Format the array, if the array longer than parameter length, then slice it
            /// </summary>
            /// <param name="arr">array to be format</param>
            /// <param name="length">the formated length</param>
            public static void formatArray(ref byte[] arr, int length)
            {
                if (arr.Length > length)
                {
                    Byte[] destArray = new byte[length];
                    Array.Copy(arr, 0, destArray, 0, length);
                    arr = destArray;
                }
            }
           
            /// <summary>
            /// Get the bype array from string, case ignroed
            ///     The string should be formated as follows
            ///         aa-11-22-33-44-55
            ///         aa:11:22:33:44:55
            ///         aa1122334455
            /// </summary>
            /// <param name="macStr">the string to changed to byte array</param>
            /// <returns>byte array, parsed from string</returns>
            public static byte[] getByteArrayfromMacString(string macStr) 
            {
                if (null == macStr) return null;

                const char FROM_DELIMER = ':';
                const char TARGET_DELIMER = '-';
                string fromatedStr = macStr.Replace(FROM_DELIMER,TARGET_DELIMER).ToUpper();
                return PhysicalAddress.Parse(fromatedStr).GetAddressBytes();
            }
        }
       }
     
}
