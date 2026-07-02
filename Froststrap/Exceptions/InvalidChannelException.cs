using System.Net;

namespace Froststrap.Exceptions
{
    public class InvalidChannelException(HttpStatusCode? statusCode) : Exception
    {
        public HttpStatusCode? StatusCode { get; } = statusCode;
    }
}