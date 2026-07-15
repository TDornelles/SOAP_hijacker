using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;
using SummitAdapter.Dispatch;
using SummitAdapter.Models;
using SummitAdapter.Options;
using SummitAdapter.Services;
using Xunit;

namespace SummitAdapter.Tests;

public class GlpClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        public Uri? LastUri { get; private set; }
        public string? LastRequestBody { get; private set; }
        public HttpRequestHeaders? LastHeaders { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            LastHeaders = request.Headers;
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };
        }
    }

    private static (GlpClient Client, StubHandler Handler) BuildClient(
        HttpStatusCode status = HttpStatusCode.OK, string body = """{ "FreightCost": 1 }""",
        string apiKey = "test-key")
    {
        var handler = new StubHandler(status, body);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://glp.test") };
        var options = Microsoft.Extensions.Options.Options.Create(new GlpOptions
        {
            BaseUrl = "http://glp.test",
            RatePath = "/api/v1/shipping/rates",
            ShipPath = "/api/v1/shipments",
            ApiKey = apiKey
        });
        return (new GlpClient(http, options), handler);
    }

    private static readonly PackageRequest SampleRequest = new()
    {
        AccountNumber = "AB",
        DestinationCountryCode = "US",
        Weight = 1,
        Length = 1,
        Width = 1,
        Height = 1,
        PackageValue = 1
    };

    [Fact]
    public async Task Rate_endpoint_posts_to_rate_path()
    {
        var (client, handler) = BuildClient();

        await client.SendAsync(GlpEndpoint.Rate, SampleRequest, CancellationToken.None);

        Assert.Equal("http://glp.test/api/v1/shipping/rates", handler.LastUri!.ToString());
    }

    [Fact]
    public async Task Ship_endpoint_posts_to_ship_path()
    {
        var (client, handler) = BuildClient();

        await client.SendAsync(GlpEndpoint.Ship, SampleRequest, CancellationToken.None);

        Assert.Equal("http://glp.test/api/v1/shipments", handler.LastUri!.ToString());
    }

    [Fact]
    public async Task Ship_endpoint_sends_api_key_header()
    {
        var (client, handler) = BuildClient(apiKey: "secret-123");

        await client.SendAsync(GlpEndpoint.Ship, SampleRequest, CancellationToken.None);

        Assert.True(handler.LastHeaders!.TryGetValues("X-API-Key", out var values));
        Assert.Equal("secret-123", Assert.Single(values!));
    }

    [Fact]
    public async Task Rate_endpoint_is_public_and_sends_no_api_key()
    {
        // Rate is a public endpoint; the key must never leak onto it even when one is configured.
        var (client, handler) = BuildClient(apiKey: "secret-123");

        await client.SendAsync(GlpEndpoint.Rate, SampleRequest, CancellationToken.None);

        Assert.False(handler.LastHeaders!.Contains("X-API-Key"));
    }

    [Fact]
    public async Task Ship_without_configured_key_fails_fast()
    {
        var (client, handler) = BuildClient(apiKey: "");

        var ex = await Assert.ThrowsAsync<GlpException>(
            () => client.SendAsync(GlpEndpoint.Ship, SampleRequest, CancellationToken.None));

        Assert.Contains("API key", ex.Message);
        // Fail fast: no HTTP call should have been attempted.
        Assert.Null(handler.LastUri);
    }

    [Fact]
    public async Task Forwards_dto_as_camelcase_json()
    {
        var (client, handler) = BuildClient();

        await client.SendAsync(GlpEndpoint.Rate, SampleRequest, CancellationToken.None);

        Assert.Contains("\"accountNumber\":\"AB\"", handler.LastRequestBody);
        Assert.Contains("\"packageValue\":1", handler.LastRequestBody);
        // BoxId is echo-only SOAP state — GLP's RateRequest rejects unknown fields, so it must
        // never appear on the wire.
        Assert.DoesNotContain("boxId", handler.LastRequestBody, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Non_success_throws_glp_exception_with_status_and_body()
    {
        var (client, _) = BuildClient(HttpStatusCode.BadGateway, "downstream boom");

        var ex = await Assert.ThrowsAsync<GlpException>(
            () => client.SendAsync(GlpEndpoint.Rate, SampleRequest, CancellationToken.None));

        Assert.Equal(502, ex.StatusCode);
        Assert.Equal("downstream boom", ex.ResponseBody);
    }

    [Fact]
    public async Task Array_response_is_accepted()
    {
        var (client, _) = BuildClient(body: """[ { "FreightCost": 9 } ]""");

        var result = await client.SendAsync(GlpEndpoint.Rate, SampleRequest, CancellationToken.None);

        Assert.Equal(9m, result.FreightCost);
    }

    [Fact]
    public async Task Non_json_success_body_throws_glp_exception_not_json_exception()
    {
        // A 200 with an HTML body (e.g. a proxy error page) must surface as GlpException so the
        // handler can return a SOAP Fault instead of an unhandled 500.
        var (client, _) = BuildClient(body: "<html>gateway error</html>");

        var ex = await Assert.ThrowsAsync<GlpException>(
            () => client.SendAsync(GlpEndpoint.Rate, SampleRequest, CancellationToken.None));

        Assert.Contains("non-JSON", ex.Message);
        Assert.Equal("<html>gateway error</html>", ex.ResponseBody);
    }

    [Fact]
    public async Task SendRaw_relays_status_content_type_and_body_verbatim()
    {
        var (client, _) = BuildClient(HttpStatusCode.Created, """{"shipmentId":"SHP-9"}""");

        var raw = await client.SendRawAsync(GlpEndpoint.Ship, SampleRequest, CancellationToken.None);

        Assert.Equal(201, raw.StatusCode);
        // Content type is relayed verbatim, including the charset GLP sent.
        Assert.Contains("application/json", raw.ContentType);
        Assert.Equal("""{"shipmentId":"SHP-9"}""", raw.Body);
    }

    [Fact]
    public async Task SendRaw_does_not_throw_on_glp_error_status_and_relays_it()
    {
        // "Return whatever GLP returned" — a GLP 422 is relayed, not turned into an exception.
        var (client, _) = BuildClient(HttpStatusCode.UnprocessableEntity, """{"error":"bad line item"}""");

        var raw = await client.SendRawAsync(GlpEndpoint.Ship, SampleRequest, CancellationToken.None);

        Assert.Equal(422, raw.StatusCode);
        Assert.Contains("bad line item", raw.Body);
    }

    [Fact]
    public async Task SendRaw_sends_api_key_on_ship()
    {
        var (client, handler) = BuildClient(HttpStatusCode.Created, "{}", apiKey: "secret-123");

        await client.SendRawAsync(GlpEndpoint.Ship, SampleRequest, CancellationToken.None);

        Assert.True(handler.LastHeaders!.TryGetValues("X-API-Key", out var values));
        Assert.Equal("secret-123", Assert.Single(values!));
    }

    [Fact]
    public async Task SendRaw_without_configured_key_fails_fast()
    {
        var (client, handler) = BuildClient(HttpStatusCode.Created, "{}", apiKey: "");

        await Assert.ThrowsAsync<GlpException>(
            () => client.SendRawAsync(GlpEndpoint.Ship, SampleRequest, CancellationToken.None));

        Assert.Null(handler.LastUri);
    }
}
