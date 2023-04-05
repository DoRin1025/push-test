using System;

namespace Model.APNS
{
    public class ApnConfigurationException : Exception
    {
        public ApnConfigurationException(string message)
            : base(message)
        {
        }
    }
}