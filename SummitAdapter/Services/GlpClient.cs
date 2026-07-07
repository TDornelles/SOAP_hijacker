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
        var path = endpoint switch
        {
            GlpEndpoint.Rate => _options.RatePath,
            GlpEndpoint.Ship => _options.ShipPath,
            _ => throw new GlpException($"Unknown GLP endpoint '{endpoint}'.")
        };

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync(path, request, JsonOptions, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new GlpException($"Could not reach GLP at '{path}': {ex.Message}", inner: ex);
        }

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

            var result = LandedCostResult.FromJson(content)
                ?? throw new GlpException($"GLP returned an empty or unparseable body for '{path}'.",
                    (int)response.StatusCode, content);

            return result;
        }
    }
}
