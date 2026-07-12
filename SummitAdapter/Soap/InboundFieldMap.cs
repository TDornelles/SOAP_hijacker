namespace SummitAdapter.Soap;

/// <summary>
/// MAPPING #1 — the single, obvious seam for the element names inside <c>&lt;rData&gt;</c>.
///
/// CONFIRMED by a real captured Summit request (fixtures/rate-request-captured → rate-request.xml,
/// 2026-07-12). The real structure is nested, PascalCase, in the PDSRouting datacontract namespace
/// (lookups are namespace-agnostic and case-insensitive, so only local names live here):
///
/// <code>
/// &lt;rData&gt;
///   &lt;AccountNumber/&gt; &lt;DestinationCountryCode/&gt; …header fields…
///   &lt;RatePackageRequests&gt;
///     &lt;RatePackageRequest&gt;
///       &lt;Weight/&gt; &lt;WeightUOM/&gt; &lt;Length/&gt; &lt;Width/&gt; &lt;Height/&gt; &lt;DimensionUOM/&gt; &lt;PackageValue/&gt; …
///     &lt;/RatePackageRequest&gt;
///   &lt;/RatePackageRequests&gt;
/// &lt;/rData&gt;
/// </code>
///
/// Captured fields NOT listed here (WSKEY, RequestDateTime, SourceOfRequest, SubAccountNumber,
/// JobNumber, DestinationPostalCode, BoxID, Insure/InsureAmount/InsureCharge, FreightCharge,
/// CurrencyCode, and the RatePackageDetailRequest line items) are deliberately dropped: GLP's
/// RateRequest accepts exactly nine fields and rejects unknown ones (ignoreUnknown = false).
/// This is still the ONLY place for inbound element names — do not scatter them through logic.
/// </summary>
public static class InboundFieldMap
{
    // ── rData header (direct children) ──────────────────────────────────────────
    public const string AccountNumber = "AccountNumber";
    public const string DestinationCountryCode = "DestinationCountryCode";

    // ── package containers ───────────────────────────────────────────────────────
    /// <summary>Wrapper element holding one or more package elements.</summary>
    public const string PackageList = "RatePackageRequests";

    /// <summary>One package. GLP rates one package per call, so exactly one is required.</summary>
    public const string Package = "RatePackageRequest";

    // ── package fields (direct children of the package element) ─────────────────
    public const string Weight = "Weight";
    public const string WeightUOM = "WeightUOM";
    public const string Length = "Length";
    public const string Width = "Width";
    public const string Height = "Height";
    public const string DimensionUOM = "DimensionUOM";
    public const string PackageValue = "PackageValue";
}
