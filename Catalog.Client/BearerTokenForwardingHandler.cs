using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;

namespace Catalog.Client
{
    /// <summary>
    /// Forwards the inbound request's bearer token onto the outbound call to Catalog.API. Order.Api
    /// reads catalog items on behalf of the authenticated caller placing/listing orders — since
    /// Catalog.API's own endpoints require auth, this delegated token pass-through is what keeps
    /// that call working, rather than minting a separate service-to-service identity for a single
    /// synchronous hop.
    /// </summary>
    public class BearerTokenForwardingHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public BearerTokenForwardingHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var authorizationHeader = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();

            if (!string.IsNullOrEmpty(authorizationHeader) && AuthenticationHeaderValue.TryParse(authorizationHeader, out var parsed))
            {
                request.Headers.Authorization = parsed;
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
