using System;
using System.Security.Cryptography;

namespace Model.Utils
{
    public class CryptoUtil
    {
        private static readonly RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
        private static byte[] uint32Buffer = new byte[4];

        private static char[] alphanumericCharacters =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();

        /// <summary>
        /// This will produce cryptographically random alphanumeric (A-Za-z0-9) string with specified length.
        /// The result string is safe for URLs.
        /// </summary>
        /// <param name="length">length of random string</param>
        /// <returns>cryptographically random URL-safe string of specified length</returns>
        public static string GetRandomAlphanumericString(int length)
        {
            return GetRandomString(length, alphanumericCharacters);
        }

        /// <summary>
        /// Based on this StackOverflow post
        /// http://stackoverflow.com/questions/1344221/how-can-i-generate-random-8-character-alphanumeric-strings-in-c/13416143#13416143
        /// </summary>
        public static string GetRandomString(int length, char[] characterArray)
        {
            if (length <= 0)
                throw new ArgumentException("length must be positive", "length");

            if (length > int.MaxValue / 4) // 500 million chars ought to be enough for anybody
                throw new ArgumentException("length is too big", "length");

            if (characterArray == null)
                throw new ArgumentNullException("characterSet");

            if (characterArray.Length == 0)
                throw new ArgumentException("characterArray must not be empty", "characterArray");

            byte[] bytes = new byte[length * 4];
            rng.GetBytes(bytes);
            char[] result = new char[length];

            for (int i = 0; i < length; i++)
            {
                uint value = BitConverter.ToUInt32(bytes, i * 4);
                result[i] = characterArray[value % characterArray.Length];
            }

            return new string(result);
        }

        /// <summary>
        /// This method is based on the following article
        /// http://msdn.microsoft.com/en-us/magazine/cc163367.aspx
        /// </summary
        public static int GetRandomInt()
        {
            rng.GetBytes(uint32Buffer);
            return BitConverter.ToInt32(uint32Buffer, 0) & 0x7FFFFFFF;
        }

        /// <summary>
        /// This method is based on the following article
        /// http://msdn.microsoft.com/en-us/magazine/cc163367.aspx
        /// </summary
        public static int GetRandomInt(int maxValue)
        {
            if (maxValue < 0)
                throw new ArgumentOutOfRangeException("maxValue");

            return GetRandomInt(0, maxValue);
        }

        /// <summary>
        /// This method is based on the following article
        /// http://msdn.microsoft.com/en-us/magazine/cc163367.aspx
        /// </summary
        public static int GetRandomInt(int minValue, int maxValue)
        {
            if (minValue > maxValue)
                throw new ArgumentOutOfRangeException("minValue");

            if (minValue == maxValue)
                return minValue;

            Int64 diff = maxValue - minValue;

            while (true)
            {
                rng.GetBytes(uint32Buffer);
                UInt32 rand = BitConverter.ToUInt32(uint32Buffer, 0);
                Int64 max = (1 + (Int64) UInt32.MaxValue);
                Int64 remainder = max % diff;
                if (rand < max - remainder)
                {
                    return (Int32) (minValue + (rand % diff));
                }
            }
        }

        /// <summary>
        /// This method is based on the following article
        /// http://msdn.microsoft.com/en-us/magazine/cc163367.aspx
        /// </summary>
        public static double GetRandomDouble()
        {
            rng.GetBytes(uint32Buffer);
            UInt32 rand = BitConverter.ToUInt32(uint32Buffer, 0);
            return rand / (1.0 + UInt32.MaxValue);
        }
    }
}