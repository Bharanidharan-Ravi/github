using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WGAPP.DomainLayer.ErrorException
{
    public class Exceptionlist
    {
        public Exceptionlist() 
        {
        
        }
        #region error exception 
        public class DataNotFoundException : Exception
        {
            public DataNotFoundException() { }

            public DataNotFoundException(string message)
                : base(message) { }

            public DataNotFoundException(string message, Exception inner)
                : base(message, inner) { }
        }
        public class InvalidDataException : Exception
        {
            public InvalidDataException() { }

            public InvalidDataException(string message)
                : base(message) { }

            public InvalidDataException(string message, Exception inner)
                : base(message, inner) { }
        }

        public class UnauthorizedException : Exception
        {
            public UnauthorizedException() { }

            public UnauthorizedException(string message)
                : base(message) { }

            public UnauthorizedException(string message, Exception inner)
                : base(message, inner) { }
        }

        // LoginException specifically for login errors with a status code
        public class LoginException : Exception
        {
            public string Username { get; }
            public string DeviceInfo { get; }
            public string Password { get; }

            public LoginException(string message, string username, string deviceInfo, string password)
                : base(message)
            {
                Username = username;
                DeviceInfo = deviceInfo;
                Password = password;
            }
        }
        #endregion
    }
}
