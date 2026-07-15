using System.Text.Json;

namespace SummitAdapter.Models;

/// <summary>
/// The rate/ship figures returned by GLP (section 6). GLP returns either a single object or an
/// array whose first element is used; <see cref="FromJson"/> handles both shapes. Properties are
/// nullable so a missing figure is simply omitted from the SOAP Result rather than emitted as 0.
/// </summary>
public sealed class LandedCostResult
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        // GLP serializes money figures as JSON strings ("FreightCost":"22.92") while the weights
        // are numbers — confirmed by live capture (fixtures/glp-rate-live-capture-2026-07-15.md).
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    public decimal? FreightCost { get; set; }
    public decimal? FuelSurcharge { get; set; }
    public decimal? TotalFreightCost { get; set; }
    public decimal? DutyValue { get; set; }
    public decimal? VatValue { get; set; }
    public decimal? TotalTaxesDuties { get; set; }
    public decimal? TotalCost { get; set; }
    public string? CurrencyCode { get; set; }
    public decimal? BillableWeight { get; set; }
    public string? BillableWeightUOM { get; set; }
    public decimal? DimensionalWeight { get; set; }

    /// <summary>
    /// Parse GLP's response body. Accepts a single object or an array (first element used).
    /// Returns null for an empty array / empty body.
    /// </summary>
    public static LandedCostResult? FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(json);
        var element = doc.RootElement;

        if (element.ValueKind == JsonValueKind.Array)
        {
            if (element.GetArrayLength() == 0)
            {
                return null;
            }

            element = element[0];
        }

        return element.Deserialize<LandedCostResult>(JsonOptions);
    }
}
