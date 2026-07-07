using System.Globalization;
using SummitAdapter.Models;

namespace SummitAdapter.Soap;

/// <summary>
/// STUBBED MAPPING #2 (spec section 4.2) — the single, obvious seam for the element names inside
/// <c>&lt;{Operation}Result&gt;</c>.
///
/// Every captured sample response was empty, so the real tag names are unknown. Until one real
/// Summit response is captured, we default to PascalCase tags matching the landed-cost field names
/// (section 6). Null figures are omitted rather than emitted as 0.
///
/// TODO(confirm): replace the tag names below with the real ones from a captured response. This is
/// the ONLY place to change them — do not scatter Result tag names through the response builder.
/// </summary>
public static class OutboundFieldMap
{
    /// <summary>Result child elements, in document order. Null values are skipped by the builder.</summary>
    public static IEnumerable<(string Tag, string? Value)> Elements(LandedCostResult r)
    {
        yield return ("FreightCost", Num(r.FreightCost));
        yield return ("FuelSurcharge", Num(r.FuelSurcharge));
        yield return ("TotalFreightCost", Num(r.TotalFreightCost));
        yield return ("DutyValue", Num(r.DutyValue));
        yield return ("VatValue", Num(r.VatValue));
        yield return ("TotalTaxesDuties", Num(r.TotalTaxesDuties));
        yield return ("TotalCost", Num(r.TotalCost));
        yield return ("CurrencyCode", r.CurrencyCode);
        yield return ("BillableWeight", Num(r.BillableWeight));
        yield return ("BillableWeightUOM", r.BillableWeightUOM);
        yield return ("DimensionalWeight", Num(r.DimensionalWeight));
    }

    private static string? Num(decimal? value) =>
        value?.ToString(CultureInfo.InvariantCulture);
}
