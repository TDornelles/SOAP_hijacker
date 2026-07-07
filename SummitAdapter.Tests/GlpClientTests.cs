using System.Net;
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

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };
        }
    }

    private static (GlpClient Client, StubHandler Handler) BuildClient(
        HttpStatusCode status = HttpStatusCode.OK, string body = """{ "FreightCost": 1 }""")
    {
        var handler = new StubHandler(status, body);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://glp.test") };
        var options = Microsoft.Extensions.Options.Options.Create(new GlpOptions
        {
            BaseUrl = "http://glp.test",
            RatePath = "/api/rate",
            ShipPath = "/api/ship"
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

        Assert.Equal("http://glp.test/api/rate", handler.LastUri!.ToString());
    }

    [Fact]
    public async Task Ship_endpoint_posts_to_ship_path()
    {
        var (client, handler) = BuildClient();

        await client.SendAsync(GlpEndpoint.Ship, SampleRequest, CancellationToken.None);

        Assert.Equal("http://glp.test/api/ship", handler.LastUri!.ToString());
    }

    [Fact]
    public async Task Forwards_dto_as_camelcase_json()
    {
        var (client, handler) = BuildClient();

        await client.SendAsync(GlpEndpoint.Rate, SampleRequest, CancellationToken.None);

        Assert.Contains("\"accountNumber\":\"AB\"", handler.LastRequestBody);
        Assert.Contains("\"packageValue\":1", handler.LastRequestBody);
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
}
