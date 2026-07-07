using SummitAdapter.Dispatch;
using SummitAdapter.Soap;
using Xunit;

namespace SummitAdapter.Tests;

public class OperationResolutionTests
{
    [Theory]
    [InlineData("http://tempuri.org/IRouting/RateLandedCost", "RateLandedCost")]
    [InlineData("\"http://tempuri.org/IRouting/RouteDeliveryRateLandedCost\"", "RouteDeliveryRateLandedCost")]
    [InlineData("RateLandedCost", "RateLandedCost")]
    public void SoapAction_header_is_parsed_to_the_last_segment(string header, string expected)
    {
        Assert.Equal(expected, OperationResolver.FromSoapAction(header));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\"\"")]
    [InlineData("   ")]
    public void Empty_or_missing_SoapAction_yields_null(string? header)
    {
        Assert.Null(OperationResolver.FromSoapAction(header));
    }

    [Fact]
    public void Falls_back_to_body_element_when_header_absent()
    {
        const string body = """
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body>
                <RateLandedCost xmlns="http://tempuri.org/"><rData/></RateLandedCost>
              </soap:Body>
            </soap:Envelope>
            """;

        Assert.Equal("RateLandedCost", OperationResolver.Resolve(soapAction: null, body));
    }

    [Fact]
    public void Header_wins_over_body_element()
    {
        const string body = """
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body>
                <SomethingElse xmlns="http://tempuri.org/"><rData/></SomethingElse>
              </soap:Body>
            </soap:Envelope>
            """;

        Assert.Equal(
            "RateLandedCost",
            OperationResolver.Resolve("http://tempuri.org/IRouting/RateLandedCost", body));
    }

    [Fact]
    public void Unknown_operation_is_not_in_the_registry()
    {
        var registry = new OperationRegistry();
        Assert.False(registry.TryGet("NoSuchOp", out _));
    }

    [Theory]
    // Ported → translated to GLP's Rate endpoint (read-only, no write).
    [InlineData("Rate", GlpEndpoint.Rate)]
    [InlineData("RateLandedCost", GlpEndpoint.Rate)]
    public void Ported_operations_translate_to_their_glp_endpoint(string name, GlpEndpoint endpoint)
    {
        var registry = new OperationRegistry();

        Assert.True(registry.TryGet(name, out var descriptor));
        Assert.Equal(OperationRouting.Translate, descriptor.Routing);
        Assert.Equal(endpoint, descriptor.GlpEndpoint);
    }

    [Theory]
    // Not yet ported → forwarded raw to the legacy service; no GLP endpoint assigned.
    [InlineData("RouteDelivery")]
    [InlineData("RouteDeliveryRateLandedCost")]
    [InlineData("RouteVoidDelivery")]
    [InlineData("GetTrackingHistory")]
    [InlineData("CloseEODProcess")]
    public void Unported_operations_pass_through_to_legacy(string name)
    {
        var registry = new OperationRegistry();

        Assert.True(registry.TryGet(name, out var descriptor));
        Assert.Equal(OperationRouting.PassThrough, descriptor.Routing);
        Assert.Null(descriptor.GlpEndpoint);
    }

    [Fact]
    public void Registry_lookup_is_case_insensitive()
    {
        var registry = new OperationRegistry();
        Assert.True(registry.TryGet("ratelandedcost", out _));
    }
}
