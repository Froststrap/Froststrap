using System;

namespace Froststrap.Exceptions
{
    internal class InvalidHTTPResponseException : Exception
    {
        public InvalidHTTPResponseException() : base() { }

        public InvalidHTTPResponseException(string message) : base(message) { }

        public InvalidHTTPResponseException(string message, Exception innerException) : base(message, innerException) { }
    }
}