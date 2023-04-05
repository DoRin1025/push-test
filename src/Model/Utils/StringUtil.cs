using System;

namespace Model.Utils
{
    public class StringUtil
    {
        
        public static byte[] HexStringToByteArray(String hexString)
        {
            byte[] bytes = new byte[hexString.Length / 2];
            for (int i = 0; i < hexString.Length; i += 2)
                bytes[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);

            return bytes;
        }

        public static string ByteArrayToHexString(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "");
        }

        public static int ParseInt(string intString, int defaultValue)
        {
            int value = defaultValue;
            if (!int.TryParse(intString, out value))
            {
                value = defaultValue;
            }

            return value;
        }

        /// <summary>
        /// Source: http://www.dotnetperls.com/string-occurrence
        /// </summary>
        public static int CountStringOccurrences(string text, string pattern)
        {
            // Loop through all instances of the string 'text'.
            int count = 0;
            int i = 0;
            while ((i = text.IndexOf(pattern, i)) != -1)
            {
                i += pattern.Length;
                count++;
            }
            return count;
        }
    }
}