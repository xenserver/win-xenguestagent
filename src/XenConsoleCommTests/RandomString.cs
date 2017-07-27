using System;
using System.Text;

namespace XenConsoleComm.Tests.Helpers
{
    public class RandomString
    {
        private static readonly UTF8Encoding UTF8Enc = new UTF8Encoding(false);
        private static readonly Random Rnd = new Random();
        private static readonly char[][] UnicodePoints;

        static RandomString()
        {
            UnicodePoints = new char[3][];
            UnicodePoints[0] = new char[52]; // Latin
            UnicodePoints[1] = new char[61]; // Greek
            UnicodePoints[2] = new char[96]; // Katakana

            int i;
            char c;

            i = 0;
            for (c = '\u0041'; c <= '\u005A'; ++c, ++i)
            {
                UnicodePoints[0][i] = c;
            }

            for (c = '\u0061'; c <= '\u007A'; ++c, ++i)
            {
                UnicodePoints[0][i] = c;
            }

            i = 0;
            for (c = '\u0391'; c <= '\u03A1'; ++c, ++i)
            {
                UnicodePoints[1][i] = c;
            }
            for (c = '\u03A3'; c <= '\u03CE'; ++c, ++i)
            {
                UnicodePoints[1][i] = c;
            }

            i = 0;
            for (c = '\u30A0'; c <= '\u30FF'; ++c, ++i)
            {
                UnicodePoints[2][i] = c;
            }
        }

        /// <summary>
        /// Creates a string of random characters (Latin, Greek, Katakana) 
        /// that is exactly 'size' bytes when converted to UTF-8 encoding.
        /// </summary>
        /// <param name="size">
        /// The size of the random string in bytes, when encoded in UTF-8
        /// </param>
        /// <returns>A 'string' of random characters</returns>
        public static string Generate(int size)
        {
            StringBuilder sb = new StringBuilder();
            int bytesLeft = size;
            while (bytesLeft != 0)
            {
                int group = Rnd.Next(UnicodePoints.Length);
                char c = UnicodePoints[group][Rnd.Next(UnicodePoints[group].Length)];
                int cBytes = UTF8Enc.GetByteCount(c.ToString());

                if (cBytes > bytesLeft)
                    continue;

                bytesLeft -= cBytes;
                sb.Append(c);
            }

            return sb.ToString();
        }
    }
}
