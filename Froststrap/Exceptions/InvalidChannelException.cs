namespace Froststrap.Exceptions
{
    internal class InvalidChannelException : Exception
    {
        public HttpStatusCode? StatusCode { get; }

        public InvalidChannelException() : base() { }

        public InvalidChannelException(string message) : base(message) { }

        public InvalidChannelException(string message, Exception innerException) : base(message, innerException) { }

        public InvalidChannelException(HttpStatusCode? statusCode) : base()
        {
            StatusCode = statusCode;
        }
    }
}