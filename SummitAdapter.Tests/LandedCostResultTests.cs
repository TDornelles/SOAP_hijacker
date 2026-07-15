using SummitAdapter.Models;
using Xunit;

namespace SummitAdapter.Tests;

public class LandedCostResultTests
{
    [Fact]
    public void Parses_single_object()
    {
        const string json = """{ "FreightCost": 10.5, "CurrencyCode": "USD" }""";

        var result = LandedCostResult.FromJson(json);

        Assert.NotNull(result);
        Assert.Equal(10.5m, result!.FreightCost);
        Assert.Equal("USD", result.CurrencyCode);
    }

    [Fact]
    public void Parses_array_using_first_element()
    {
        const string json = """[ { "FreightCost": 1 }, { "FreightCost": 2 } ]""";

        var result = LandedCostResult.FromJson(json);

        Assert.Equal(1m, result!.FreightCost);
    }

    [Fact]
    public void Is_property_name_case_insensitive()
    {
        const string json = """{ "freightcost": 7, "currencycode": "EUR" }""";

        var result = LandedCostResult.FromJson(json);

        Assert.Equal(7m, result!.FreightCost);
        Assert.Equal("EUR", result.CurrencyCode);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_inputs_return_null(string json)
    {
        Assert.Null(LandedCostResult.FromJson(json));
    }

    [Fact]
    public void Parses_real_glp_response_with_string_money_figures()
    {
        // Verbatim live GLP response (fixtures/glp-rate-live-capture-2026-07-15.md): money figures
        // are JSON strings, weights are numbers.
        const string json =
            """[{"FreightCost":"22.92","FuelSurcharge":"5.73","TotalFreightCost":"28.65","DutyValue":"0.00","VatValue":"0.00","TotalTaxesDuties":"0.00","TotalCost":"28.65","CurrencyCode":"USD","BillableWeight":1.11,"BillableWeightUOM":"Pounds","DimensionalWeight":1.11}]""";

        var result = LandedCostResult.FromJson(json);

        Assert.NotNull(result);
        Assert.Equal(22.92m, result!.FreightCost);
        Assert.Equal(5.73m, result.FuelSurcharge);
        Assert.Equal(28.65m, result.TotalCost);
        Assert.Equal(0m, result.DutyValue);
        Assert.Equal("USD", result.CurrencyCode);
        Assert.Equal(1.11m, result.BillableWeight);
        Assert.Equal("Pounds", result.BillableWeightUOM);
    }
}
