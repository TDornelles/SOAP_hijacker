using System.Xml.Linq;
using SummitAdapter.Dispatch;
using SummitAdapter.Models;
using SummitAdapter.Soap;
using Xunit;

namespace SummitAdapter.Tests;

/// <summary>
/// Asserts the builder reproduces the legacy Result shape captured live on 2026-07-15
/// (fixtures/soap-rate-live-capture-2026-07-15.md): nested PDSRouting entries, echoed
/// AccountNumber/BoxID, the decided GLP→legacy figure mapping, and the success MessageEntity.
/// </summary>
public class SoapResponseBuilderTests
{
    private static readonly XNamespace Tempuri = SoapConstants.TempuriNamespace;
    private static readonly XNamespace Pds = SoapConstants.PdsRoutingNamespace;
    private static readonly XNamespace Xsi = SoapConstants.XsiNamespace;

    private readonly SoapResponseBuilder _builder = new();

    private static OperationDescriptor Descriptor(string name) =>
        new(name, OperationRouting.Translate, GlpEndpoint.Rate, SoapConstants.TempuriNamespace);

    private static PackageRequest Request(string account = "3528", string boxId = "0") => new()
    {
        AccountNumber = account,
        DestinationCountryCode = "NZ",
        Weight = 0.45m,
        Length = 11m,
        Width = 7m,
        Height = 2m,
        PackageValue = 12.99m,
        BoxId = boxId,
    };

    /// <summary>The live GLP figures from the 2026-07-15 capture.</summary>
    private static LandedCostResult CapturedGlpResult() => new()
    {
        FreightCost = 22.92m,
        FuelSurcharge = 5.73m,
        TotalFreightCost = 28.65m,
        DutyValue = 0m,
        VatValue = 0m,
        TotalTaxesDuties = 0m,
        TotalCost = 28.65m,
        CurrencyCode = "USD",
        BillableWeight = 1.11m,
        BillableWeightUOM = "Pounds",
        DimensionalWeight = 1.11m,
    };

    private XElement BuildEntry(LandedCostResult result, PackageRequest? request = null)
    {
        var xml = _builder.Build(Descriptor("RateLandedCost"), request ?? Request(), result);
        var doc = XDocument.Parse(xml);
        return doc.Descendants(Pds + "RateResponseEntry").Single();
    }

    [Fact]
    public void Builds_the_captured_nested_structure()
    {
        var xml = _builder.Build(Descriptor("RateLandedCost"), Request(), CapturedGlpResult());

        var doc = XDocument.Parse(xml); // parses => well-formed
        var result = doc.Descendants(Tempuri + "RateLandedCostResult").Single();

        Assert.Equal("3528", result.Element(Pds + "AccountNumber")!.Value);
        Assert.NotNull(result.Element(Pds + "ResponseDateTime"));

        var entry = result.Element(Pds + "RateResponseEntries")!.Element(Pds + "RateResponseEntry")!;
        Assert.Equal("0", entry.Element(Pds + "BoxID")!.Value);

        var rate = entry.Element(Pds + "RateEntity")!;
        Assert.Equal("0", rate.Element(Pds + "ProcessingCost")!.Value);
        Assert.Equal("1.11", rate.Element(Pds + "BillableWeight")!.Value);
        Assert.Equal("Pounds", rate.Element(Pds + "BillableWeightUOM")!.Value);
        Assert.Equal("0", rate.Element(Pds + "InsureCost")!.Value);
        Assert.Equal("USD", rate.Element(Pds + "CurrencyCode")!.Value);

        var message = entry.Element(Pds + "MessageEntities")!.Element(Pds + "MessageEntity")!;
        Assert.Equal("00000", message.Element(Pds + "Code")!.Value);
        Assert.Equal("Request Successful", message.Element(Pds + "Description")!.Value);
    }

    [Fact]
    public void FreightCost_carries_the_fuel_inclusive_total()
    {
        // DECISION (2026-07-15): GLP's FuelSurcharge must not be dropped — FreightCost gets
        // TotalFreightCost (28.65), not the base freight (22.92).
        var rate = BuildEntry(CapturedGlpResult()).Element(Pds + "RateEntity")!;

        Assert.Equal("28.65", rate.Element(Pds + "FreightCost")!.Value);
    }

    [Fact]
    public void FreightCost_falls_back_to_base_freight_when_total_is_absent()
    {
        var rate = BuildEntry(new LandedCostResult { FreightCost = 22.92m })
            .Element(Pds + "RateEntity")!;

        Assert.Equal("22.92", rate.Element(Pds + "FreightCost")!.Value);
    }

    [Fact]
    public void LandedCost_carries_the_taxes_and_duties_total()
    {
        var rate = BuildEntry(new LandedCostResult { TotalTaxesDuties = 21.40m })
            .Element(Pds + "RateEntity")!;

        Assert.Equal("21.40", rate.Element(Pds + "LandedCost")!.Value);
    }

    [Fact]
    public void BoxId_and_account_are_echoed_from_the_request()
    {
        var xml = _builder.Build(
            Descriptor("RateLandedCost"),
            Request(account: "9999", boxId: "7"),
            CapturedGlpResult());

        var doc = XDocument.Parse(xml);
        Assert.Equal("9999", doc.Descendants(Pds + "AccountNumber").Single().Value);
        Assert.Equal("7", doc.Descendants(Pds + "BoxID").Single().Value);
    }

    [Fact]
    public void LandedCostDetailEntities_is_emitted_nil()
    {
        // Per-line duty/tax detail cannot be reconstructed (line items are dropped inbound);
        // legacy's nil marker is emitted instead. The duties total lives in LandedCost.
        var details = BuildEntry(CapturedGlpResult()).Element(Pds + "LandedCostDetailEntities")!;

        Assert.Equal("true", details.Attribute(Xsi + "nil")!.Value);
        Assert.Empty(details.Elements());
    }

    [Fact]
    public void ResponseDateTime_is_a_valid_timestamp()
    {
        var entryDoc = _builder.Build(Descriptor("RateLandedCost"), Request(), CapturedGlpResult());
        var value = XDocument.Parse(entryDoc).Descendants(Pds + "ResponseDateTime").Single().Value;

        Assert.True(System.DateTimeOffset.TryParse(value, out _));
    }

    [Fact]
    public void Result_element_name_tracks_the_operation()
    {
        var xml = _builder.Build(Descriptor("RouteDeliveryRateLandedCost"), Request(), CapturedGlpResult());
        Assert.Contains("RouteDeliveryRateLandedCostResult", xml);
        Assert.Contains("RouteDeliveryRateLandedCostResponse", xml);
    }

    [Fact]
    public void Null_figures_are_omitted()
    {
        var rate = BuildEntry(new LandedCostResult { TotalFreightCost = 1.0m })
            .Element(Pds + "RateEntity")!;

        Assert.NotNull(rate.Element(Pds + "FreightCost"));
        Assert.Null(rate.Element(Pds + "LandedCost"));
        Assert.Null(rate.Element(Pds + "BillableWeight"));
        Assert.Null(rate.Element(Pds + "CurrencyCode"));
    }

    [Fact]
    public void DimensionFactor_is_never_emitted()
    {
        // GLP does not expose a dim divisor; inventing legacy's 139 would be fabricating data.
        var xml = _builder.Build(Descriptor("RateLandedCost"), Request(), CapturedGlpResult());
        Assert.DoesNotContain("DimensionFactor", xml);
    }

    [Fact]
    public void Special_characters_in_string_values_are_escaped()
    {
        var result = CapturedGlpResult();
        result.CurrencyCode = "A&B<C>\"D\"";

        var xml = _builder.Build(Descriptor("RateLandedCost"), Request(), result);

        // Raw ampersand/angle brackets must not appear unescaped in the payload.
        Assert.Contains("A&amp;B&lt;C&gt;", xml);
        // And it must still round-trip to the original value.
        var doc = XDocument.Parse(xml);
        Assert.Equal("A&B<C>\"D\"", doc.Descendants(Pds + "CurrencyCode").Single().Value);
    }

    [Fact]
    public void Declares_utf8_encoding()
    {
        var xml = _builder.Build(Descriptor("RateLandedCost"), Request(), CapturedGlpResult());
        Assert.Contains("encoding=\"utf-8\"", xml, System.StringComparison.OrdinalIgnoreCase);
    }
}
