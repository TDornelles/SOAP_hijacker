using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SummitAdapter.Services;
using SummitAdapter.Soap;
using SummitAdapter.Tests.TestSupport;
using Xunit;

namespace SummitAdapter.Tests;

/// <summary>
/// SOAP in → SOAP out through the real in-memory host, with GLP mocked. Covers one Rate op and one
/// Route op end-to-end (section 9).
/// </summary>
public class EndToEndTests : IClassFixture<EndToEndTests.AdapterFactory>
{
    private const string Path = "/Routing/Service/Soap/V2.6/Routing.svc";
    private readonly AdapterFactory _factory;

    public EndToEndTests(AdapterFactory factory) => _factory = factory;

    public sealed class AdapterFactory : WebApplicationFactory<Program>
    {
        public FakeGlpClient Glp { get; } = new();
        public FakeLegacyForwarder Legacy { get; } =
            new(new LegacyResponse(200, "text/xml; charset=utf-8",
                "<soap:Envelope><soap:Body><RouteDeliveryRateLandedCostResponse/></soap:Body></soap:Envelope>"));

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGlpClient>();
                services.AddSingleton<IGlpClient>(Glp);
                services.RemoveAll<ILegacyForwarder>();
                services.AddSingleton<ILegacyForwarder>(Legacy);
                // Don't write audit files during tests.
                services.Configure<SummitAdapter.Options.AuditOptions>(o => o.Enabled = false);
            });
            return base.CreateHost(builder);
        }
    }

    private static StringContent SoapContent(string xml) =>
        new(xml, Encoding.UTF8, "text/xml");

    private HttpRequestMessage SoapRequest(string xml, string soapAction)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Path) { Content = SoapContent(xml) };
        request.Content!.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };
        request.Headers.TryAddWithoutValidation("SOAPAction", soapAction);
        return request;
    }

    [Fact]
    public async Task Rate_op_returns_soap_result()
    {
        var client = _factory.CreateClient();

        var response = await client.SendAsync(
            SoapRequest(Fixtures.Load(Fixtures.RateRequest), "http://tempuri.org/IRouting/RateLandedCost"));

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("text/xml", response.Content.Headers.ContentType!.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        var doc = XDocument.Parse(body);
        XNamespace tempuri = SoapConstants.TempuriNamespace;
        XNamespace pds = SoapConstants.PdsRoutingNamespace;

        // The Result reproduces the captured legacy shape: account + BoxID echoed from the
        // fixture request, figures from the (fake) GLP result nested under RateEntity, and the
        // fuel-inclusive TotalFreightCost in the FreightCost slot.
        var result = doc.Descendants(tempuri + "RateLandedCostResult").Single();
        Assert.Equal("3528", result.Element(pds + "AccountNumber")!.Value);

        var entry = result.Element(pds + "RateResponseEntries")!.Element(pds + "RateResponseEntry")!;
        Assert.Equal("0", entry.Element(pds + "BoxID")!.Value);

        var rate = entry.Element(pds + "RateEntity")!;
        Assert.Equal("USD", rate.Element(pds + "CurrencyCode")!.Value);
        Assert.Equal("47.60", rate.Element(pds + "FreightCost")!.Value);
        Assert.Equal("21.40", rate.Element(pds + "LandedCost")!.Value);

        var message = entry.Element(pds + "MessageEntities")!.Element(pds + "MessageEntity")!;
        Assert.Equal("00000", message.Element(pds + "Code")!.Value);
    }

    [Fact]
    public async Task Route_op_is_passed_through_to_legacy_verbatim()
    {
        var client = _factory.CreateClient();

        // The Ship path is not ported yet, so a Route (new package insertion) request must be
        // relayed to the legacy service untouched and its response returned verbatim.
        var response = await client.SendAsync(
            SoapRequest(Fixtures.Load(Fixtures.RouteRequest),
                "http://tempuri.org/IRouting/RouteDeliveryRateLandedCost"));

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("text/xml", response.Content.Headers.ContentType!.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("RouteDeliveryRateLandedCostResponse", body);
        Assert.Empty(_factory.Glp.Calls);
        Assert.Single(_factory.Legacy.Calls);
    }

    [Fact]
    public async Task Health_endpoint_returns_ok()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("OK", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Malformed_request_returns_soap_fault_with_xml_content_type()
    {
        var client = _factory.CreateClient();

        var response = await client.SendAsync(SoapRequest("<broken", "RateLandedCost"));

        Assert.Equal("text/xml", response.Content.Headers.ContentType!.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("soap:Fault", body);
        Assert.Contains("soap:Client", body);
    }
}
