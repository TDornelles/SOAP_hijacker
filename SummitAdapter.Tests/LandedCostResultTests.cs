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
}
