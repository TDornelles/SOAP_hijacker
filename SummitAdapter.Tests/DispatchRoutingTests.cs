using SummitAdapter.Dispatch;
using SummitAdapter.Tests.TestSupport;
using Xunit;

namespace SummitAdapter.Tests;

/// <summary>
/// Routing at the handler level for the selective hijacker. Only ported operations are translated
/// to GLP (today: the Rate family → Rate endpoint, never Ship). Everything else — Route operations
/// not yet ported, tracking/EOD, and any un-enumerated SOAPAction — is forwarded to the legacy
/// service untouched. Asserted by capturing which downstream (GLP vs legacy) is called.
/// </summary>
public class DispatchRoutingTests
{
    [Fact]
    public async Task Rate_family_translates_to_rate_and_never_ship()
    {
        var glp = new FakeGlpClient();
        var legacy = new FakeLegacyForwarder();

        await HandlerHarness.InvokeAsync(
            glp, Fixtures.Load(Fixtures.RateRequest), "http://tempuri.org/IRouting/RateLandedCost", legacy);

        Assert.Single(glp.Calls);
        Assert.Equal(GlpEndpoint.Rate, glp.LastEndpoint);
        Assert.DoesNotContain(glp.Calls, c => c.Endpoint == GlpEndpoint.Ship);
        Assert.Empty(legacy.Calls);
    }

    [Fact]
    public async Task Route_family_passes_through_to_legacy_and_does_not_call_glp()
    {
        var glp = new FakeGlpClient();
        var legacy = new FakeLegacyForwarder();

        var result = await HandlerHarness.InvokeAsync(
            glp, Fixtures.Load(Fixtures.RouteRequest),
            "http://tempuri.org/IRouting/RouteDeliveryRateLandedCost", legacy);

        // Not ported yet → forwarded raw to legacy, GLP untouched, legacy response relayed verbatim.
        Assert.Empty(glp.Calls);
        var call = Assert.Single(legacy.Calls);
        Assert.Equal("http://tempuri.org/IRouting/RouteDeliveryRateLandedCost", call.SoapAction);
        Assert.Contains("relayed", result.Body);
    }

    [Fact]
    public async Task Unknown_operation_passes_through_to_legacy()
    {
        var glp = new FakeGlpClient();
        var legacy = new FakeLegacyForwarder();

        await HandlerHarness.InvokeAsync(
            glp, Fixtures.Load(Fixtures.RateRequest), "http://tempuri.org/IRouting/DoesNotExist", legacy);

        // An un-enumerated SOAPAction must not break Summit — it is relayed to the legacy service.
        Assert.Empty(glp.Calls);
        Assert.Single(legacy.Calls);
    }

    [Fact]
    public async Task Tracking_operation_passes_through_to_legacy()
    {
        var glp = new FakeGlpClient();
        var legacy = new FakeLegacyForwarder();

        await HandlerHarness.InvokeAsync(
            glp, Fixtures.Load(Fixtures.RateRequest), "http://tempuri.org/IRouting/GetTrackingHistory", legacy);

        Assert.Empty(glp.Calls);
        Assert.Single(legacy.Calls);
    }

    [Fact]
    public async Task Legacy_outage_on_pass_through_returns_server_fault()
    {
        var glp = new FakeGlpClient();
        var legacy = new FakeLegacyForwarder(@throw: true);

        var result = await HandlerHarness.InvokeAsync(
            glp, Fixtures.Load(Fixtures.RouteRequest),
            "http://tempuri.org/IRouting/RouteDeliveryRateLandedCost", legacy);

        Assert.Contains("soap:Server", result.Body);
        Assert.Contains("text/xml", result.ContentType);
    }

    [Fact]
    public async Task Malformed_xml_on_ported_operation_returns_client_fault()
    {
        var glp = new FakeGlpClient();

        // RateLandedCost is ported, so its body is parsed — malformed XML → Client fault.
        var result = await HandlerHarness.InvokeAsync(glp, "<broken", "RateLandedCost");

        Assert.Contains("soap:Client", result.Body);
        Assert.Contains("text/xml", result.ContentType);
    }
}
