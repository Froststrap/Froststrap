namespace Froststrap
{
    internal class HttpClientLoggingHandler(HttpMessageHandler innerHandler)
        : MessageProcessingHandler(innerHandler)
    {
        protected override HttpRequestMessage ProcessRequest(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            App.Logger.Info($"{request.Method} {request.RequestUri}");
            return request;
        }

        protected override HttpResponseMessage ProcessResponse(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            App.Logger.Info($"{(int)response.StatusCode} {response.ReasonPhrase} {response.RequestMessage!.RequestUri}");
            return response;
        }
    }
}
