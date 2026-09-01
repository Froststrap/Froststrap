namespace Froststrap.Exceptions
{
    internal class HttpResponseException : Exception
    {
        public HttpResponseMessage? ResponseMessage { get; }

        public HttpResponseException() : base() { }

        public HttpResponseException(string message) : base(message) { }

        public HttpResponseException(string message, Exception innerException) : base(message, innerException) { }

        public HttpResponseException(HttpResponseMessage responseMessage)
            : base($"Could not connect to {responseMessage.RequestMessage?.RequestUri} because it returned HTTP {(int)responseMessage.StatusCode} ({responseMessage.ReasonPhrase})")
        {
            ResponseMessage = responseMessage;
        }
    }
}