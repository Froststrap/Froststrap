namespace Froststrap.Exceptions
{
    internal class HttpResponseException : Exception
    {
        public HttpResponseMessage ResponseMessage { get; }

        public HttpResponseException(HttpResponseMessage responseMessage)
            : base($"Could not connect to {responseMessage.RequestMessage!.RequestUri} because it returned HTTP {(int)responseMessage.StatusCode} ({responseMessage.ReasonPhrase})")
        {
            ResponseMessage = responseMessage;
        }
    }
}