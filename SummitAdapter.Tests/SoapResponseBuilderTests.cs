using System.Xml.Linq;
using SummitAdapter.Dispatch;
using SummitAdapter.Models;
using SummitAdapter.Soap;
using Xunit;

namespace SummitAdapter.Tests;

public class SoapResponseBuilderTests
{
    private readonly SoapResponseBuilder _builder = new();

    private static OperationDescriptor Descriptor(string name) =>
        new(name, OperationRouting.Translate, GlpEndpoint.Rate, SoapConstants.TempuriNamespace);

    [Fact]
    public void Builds_well_formed_envelope_with_expected_result_elements()
    {
        var xml = _builder.Build(Descriptor("RateLandedCost"), new LandedCostResult
        {
            FreightCost = 42.50m,
            TotalCost = 69.00m,
            CurrencyCode = "USD",
            BillableWeightUOM = "Pounds"
        });

        var doc = XDocument.Parse(xml); // parses => well-formed
        XNamespace tempuri = SoapConstants.TempuriNamespace;

        var result = doc.Descendants(tempuri + "RateLandedCostResult").Single();
        Assert.Equal("42.50", result.Element(tempuri + "FreightCost")!.Value);
        Assert.Equal("69.00", result.Element(tempuri + "TotalCost")!.Value);
        Assert.Equal("USD", result.Element(tempuri + "CurrencyCode")!.Value);
        Assert.Equal("Pounds", result.Element(tempuri + "BillableWeightUOM")!.Value);
    }

    [Fact]
    public void Result_element_name_tracks_the_operation()
    {
        var xml = _builder.Build(Descriptor("RouteDeliveryRateLandedCost"), FakeResult());
        Assert.Contains("RouteDeliveryRateLandedCostResult", xml);
        Assert.Contains("RouteDeliveryRateLandedCostResponse", xml);
    }

    [Fact]
    public void Null_figures_are_omitted()
    {
        var xml = _builder.Build(Descriptor("RateLandedCost"), new LandedCostResult
        {
            FreightCost = 1.0m
            // everything else null
        });

        Assert.DoesNotContain("<DutyValue", xml);
        Assert.Contains("FreightCost", xml);
    }

    [Fact]
    public void Special_characters_in_string_values_are_escaped()
    {
        var xml = _builder.Build(Descriptor("RateLandedCost"), new LandedCostResult
        {
            CurrencyCode = "A&B<C>\"D\""
        });

        // Raw ampersand/angle brackets must not appear unescaped in the payload.
        Assert.Contains("A&amp;B&lt;C&gt;", xml);
        // And it must still round-trip to the original value.
        var doc = XDocument.Parse(xml);
        XNamespace tempuri = SoapConstants.TempuriNamespace;
        Assert.Equal("A&B<C>\"D\"", doc.Descendants(tempuri + "CurrencyCode").Single().Value);
    }

    [Fact]
    public void Declares_utf8_encoding()
    {
        var xml = _builder.Build(Descriptor("RateLandedCost"), FakeResult());
        Assert.Contains("encoding=\"utf-8\"", xml, System.StringComparison.OrdinalIgnoreCase);
    }

    private static LandedCostResult FakeResult() => new()
    {
        FreightCost = 1m,
        CurrencyCode = "USD"
    };
}
