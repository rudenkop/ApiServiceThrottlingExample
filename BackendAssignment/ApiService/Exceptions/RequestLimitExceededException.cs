using System;

namespace CleoAssignment.ApiService.Exceptions
{
    public class RequestLimitExceededException : Exception
    {
        public RequestLimitExceededException(string ip) : base($"Request limit exceeded. IP address is temporarily banned for ip address: {ip}.")
        { }
    }

}

