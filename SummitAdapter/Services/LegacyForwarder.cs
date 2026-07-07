using System.Net.Http.Headers;
using System.Text;

namespace SummitAdapter.Services;

/// <summary>
/// Typed <see cref="HttpClient"/> that re-issues an un-ported SOAP request against the relocated
/// legacy service (BaseAddress = <c>Legacy:BaseUrl</c>) and returns its response verbatim. Stateless
/// and side-effect free from the adapter's point of view — all it does is relay bytes.
/// </summary>
public sealed class LegacyForwarder : ILegacyForwarder
{
    private readonly HttpClient _http;

    public LegacyForwarder(HttpClient http) => _http = http;

    public async Task<LegacyResponse> ForwardAsync(
        string path, string soapAction, string contentType, string body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(body, Encoding.UTF8)
        };

        // Preserve the exact content type Summit sent (text/xml; charset=utf-8) and the SOAPAction —
        // the legacy service selects the operation from that header.
        if (MediaTypeHeaderValue.TryParse(contentType, out var mediaType))
        {
            request.Content.Headers.ContentType = mediaType;
        }
        request.Headers.TryAddWithoutValidation("SOAPAction", soapAction ?? string.Empty);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new LegacyForwardException($"Could not reach the legacy service at '{path}': {ex.Message}", ex);
        }

        using (response)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseContentType = response.Content.Headers.ContentType?.ToString()
                ?? Soap.SoapConstants.XmlContentType;
            return new LegacyResponse((int)response.StatusCode, responseContentType, content);
        }
    }
}
