using System.Text.RegularExpressions;

namespace Model.Utils
{
    public class ValidationUtil
    {
        private const string SC_PUBLISHER_ID = "";

        public static bool ValidatePublisherId(string publisherId)
        {
            if (publisherId != null && publisherId.Equals(SC_PUBLISHER_ID))
                return true;

            if (string.IsNullOrWhiteSpace(publisherId))
            {
                return false;
            }

            if (publisherId.Length > 50)
            {
                return false;
            }

            for (int i = 0; i < publisherId.Length; i++)
            {
                if (!(char.IsLetterOrDigit(publisherId[i])))
                {
                    if (publisherId[i] == '-')
                    {
                        //allow
                    }
                    else
                        return false;
                }
                else if (publisherId[i] > 0x007A)
                    return false;
            }

            return true;
        }

        public static bool ValidateUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return false;
            }

            if (username.Length > 50)
            {
                return false;
            }

            for (int i = 0; i < username.Length; i++)
            {
                if (!(char.IsLetterOrDigit(username[i])))
                {
                    if (username[i] == '_')
                    {
                        //allow
                    }
                    else
                        return false;
                }
                else if (username[i] > 0x007a)
                    return false;
            }

            return true;
        }

        public static bool ValidateAppId(string appid)
        {
            if (string.IsNullOrWhiteSpace(appid))
            {
                return false;
            }

            if (appid.Length > 50)
            {
                return false;
            }

            for (int i = 0; i < appid.Length; i++)
            {
                if (!(char.IsLetterOrDigit(appid[i])))
                {
                    return false;
                }
                else if (appid[i] > 0x007a)
                    return false;
            }

            return true;
        }

        // Validating device id: only alphanumerics, and '-'; length from 16 to 36
        public static bool IsValidDeviceId(string deviceId)
        {
            return (deviceId != null && Regex.IsMatch(deviceId, "^[0-9A-Za-z-]{10,36}$"));
        }
    }
}