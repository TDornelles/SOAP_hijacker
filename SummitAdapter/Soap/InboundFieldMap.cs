namespace SummitAdapter.Soap;

/// <summary>
/// STUBBED MAPPING #1 (spec section 4.1) — the single, obvious seam for the child element names
/// inside <c>&lt;rData&gt;</c>.
///
/// Every captured sample request had an empty <c>&lt;rData/&gt;</c>, so the real element names are
/// unknown. Until one real populated Summit request is captured, we default to the JSON DTO field
/// names (camelCase, section 6), looked up namespace-agnostically and case-insensitively by
/// <see cref="SoapRequestParser"/>.
///
/// TODO(confirm): replace these constants with the real element names from a captured request.
/// This is the ONLY place to change them — do not scatter element names through parsing logic.
/// </summary>
public static class InboundFieldMap
{
    public const string AccountNumber = "accountNumber";
    public const string DestinationCountryCode = "destinationCountryCode";
    public const string Weight = "weight";
    public const string WeightUOM = "weightUOM";
    public const string Length = "length";
    public const string Width = "width";
    public const string Height = "height";
    public const string DimensionUOM = "dimensionUOM";
    public const string PackageValue = "packageValue";
}
