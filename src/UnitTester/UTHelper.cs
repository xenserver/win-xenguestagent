using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UTHelper
{
    public class UTFunctions
    {
        /// <summary>
        /// Get mac string from the byte arrar, seperated by ":"
        /// </summary>
        /// <param name="macbyte">mac array in bytes</param>
        /// <returns></returns>
        public static string getMacString(byte[] macbyte)
        {
            return string.Format("{0:x2}:{1:x2}:{2:x2}:{3:x2}:{4:x2}:{5:x2}", macbyte[0], macbyte[1], macbyte[2], macbyte[3], macbyte[4], macbyte[5]);
        }
    }
}
