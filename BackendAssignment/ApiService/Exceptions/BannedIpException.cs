using System;

namespace CleoAssignment.ApiService.Exceptions
{
    public class BannedIpException : Exception
    {
        public BannedIpException(string ip) : base($"IP address {ip} is banned due to request limits exceeding.")
        { }
    }

}

