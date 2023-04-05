using System;

namespace ApplicationCore.APNS
{
    public class ApnConfigurationException : Exception
    {
        public ApnConfigurationException(string message)
            : base(message)
        {
        }
    }
}