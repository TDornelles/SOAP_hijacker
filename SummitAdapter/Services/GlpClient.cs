using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SummitAdapter.Dispatch;
using SummitAdapter.Models;
using SummitAdapter.Options;

namespace SummitAdapter.Services;

/// <summary>
/// Typed <see cref="HttpClient"/> that POSTs the package DTO as camelCase JSON to GLP. The endpoint
/// path is resolved from <see cref="GlpOptions"/> based on the operation family — the rate family
/// always hits RatePath and never ShipPath, the route family always hits ShipPath.
/// </summary>
public sealed class GlpClient : IGlpClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _http;
    private readonly GlpOptions _options;

    public GlpClient(HttpClient http, IOptions<GlpOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<LandedCostResult> SendAsync(
        GlpEndpoint endpoint, PackageRequest request, CancellationToken cancellationToken)
    {
        var (path, response) = await PostAsync(endpoint, request, cancellationToken);

        using (response)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new GlpException(
                    $"GLP returned {(int)response.StatusCode} for '{path}'.",
                    (int)response.StatusCode,
                    content);
            }

            // A 200 with a non-JSON body (e.g. an HTML error page from a proxy) must become a
            // GlpException — the handler turns that into a SOAP Fault, never a bare HTTP page.
            LandedCostResult? result;
            try
            {
                result = LandedCostResult.FromJson(content);
            }
            catch (System.Text.Json.JsonException ex)
            {
                throw new GlpException($"GLP returned a non-JSON body for '{path}': {ex.Message}",
                    (int)response.StatusCode, content);
            }

            return result
                ?? throw new GlpException($"GLP returned an empty or unparseable body for '{path}'.",
                    (int)response.StatusCode, content);
        }
    }

    public async Task<GlpRawResponse> SendRawAsync(
        GlpEndpoint endpoint, PackageRequest request, CancellationToken cancellationToken)
    {
        var (_, response) = await PostAsync(endpoint, request, cancellationToken);

        // Relay whatever GLP returned — any status, any body. We do NOT throw on a non-success
        // status here: an error status + body from GLP is part of "what GLP returned" and is passed
        // back to the caller. Only a transport failure (handled in PostAsync) becomes a Fault.
        using (response)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
            return new GlpRawResponse((int)response.StatusCode, contentType, content);
        }
    }

    /// <summary>
    /// Resolve the endpoint path, POST the DTO as camelCase JSON, and (for Ship only) attach the
    /// <c>X-API-Key</c>. Shared by the typed and raw sends. Throws <see cref="GlpException"/> if GLP
    /// is unreachable; the caller decides how to interpret the HTTP-level response.
    /// </summary>
    private async Task<(string Path, HttpResponseMessage Response)> PostAsync(
        GlpEndpoint endpoint, PackageRequest request, CancellationToken cancellationToken)
    {
        var path = endpoint switch
        {
            GlpEndpoint.Rate => _options.RatePath,
            GlpEndpoint.Ship => _options.ShipPath,
            _ => throw new GlpException($"Unknown GLP endpoint '{endpoint}'.")
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };

        // The Ship endpoint creates a delivery (DB write) and is protected by an API key; Rate is a
        // public, keyless price check and must never carry the key. Fail fast with a clear message if
        // the Ship key is missing, rather than letting GLP answer 401 at go-live.
        if (endpoint == GlpEndpoint.Ship)
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                throw new GlpException(
                    "The GLP Ship endpoint requires an API key, but Glp:ApiKey is not configured. " +
                    "Set the Glp__ApiKey environment variable on the server.");
            }

            message.Headers.Add("X-API-Key", _options.ApiKey);
        }

        try
        {
            return (path, await _http.SendAsync(message, cancellationToken));
        }
        catch (HttpRequestException ex)
        {
            throw new GlpException($"Could not reach GLP at '{path}': {ex.Message}", inner: ex);
        }
    }
}
