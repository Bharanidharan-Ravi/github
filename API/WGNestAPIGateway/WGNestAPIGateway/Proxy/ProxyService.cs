namespace APIGateway.Proxy
{
    public class ProxyService
    {
        private readonly HttpClient _httpClient;

        public ProxyService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<HttpResponseMessage> ForwardRequestAsync(HttpRequestMessage request, string serviceName)
        {
            // Add custom headers for metadata
            request.Headers.Add("X-Service-Name", serviceName);

            // Forward the request to the actual service
            var response = await _httpClient.SendAsync(request);
            return response;
        }
    }
}