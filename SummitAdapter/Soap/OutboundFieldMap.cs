using System.Globalization;
using SummitAdapter.Models;

namespace SummitAdapter.Soap;

/// <summary>
/// MAPPING #2 (spec section 4.2) — the single, obvious seam for the element names inside
/// <c>&lt;{Operation}Result&gt;</c> and for how GLP's figures land in legacy slots.
///
/// CONFIRMED by live capture of the real V2.6 endpoint, error and success variants
/// (fixtures/soap-rate-live-capture-2026-07-15.md). The Result is NESTED in the PDSRouting
/// datacontract namespace, mirroring the request:
///
/// <code>
/// &lt;{Operation}Result&gt;                 (tempuri namespace)
///   &lt;a:AccountNumber/&gt; &lt;a:ResponseDateTime/&gt;
///   &lt;a:RateResponseEntries&gt;&lt;a:RateResponseEntry&gt;
///     &lt;a:BoxID/&gt;
///     &lt;a:RateEntity&gt; …figures, see &lt;see cref="RateEntityElements"/&gt;… &lt;/a:RateEntity&gt;
///     &lt;a:LandedCostDetailEntities i:nil="true"/&gt;
///     &lt;a:MessageEntities&gt;&lt;a:MessageEntity&gt;&lt;a:Code/&gt;&lt;a:Description/&gt;…
///   &lt;/a:RateResponseEntry&gt;&lt;/a:RateResponseEntries&gt;
/// &lt;/{Operation}Result&gt;
/// </code>
///
/// GLP's response vocabulary does not map 1:1 onto legacy's — the deliberate decisions:
/// - <b>FreightCost ← GLP TotalFreightCost</b> (fuel-inclusive; decided by the user 2026-07-15).
///   Legacy has no FuelSurcharge slot, and emitting base freight would silently drop money.
///   Falls back to GLP's base FreightCost if the total is absent.
/// - <b>LandedCost ← GLP TotalTaxesDuties</b> — legacy's slot for the duties+taxes total.
/// - <b>ProcessingCost / InsureCost = 0</b> — no GLP source; 0 is what legacy emitted, and for
///   insurance it is true (inbound insurance fields are dropped, GLP rates none).
/// - <b>DimensionFactor omitted</b> — legacy emits its dim divisor (139); GLP doesn't expose one
///   and inventing it is worse than omitting (DataContract clients default a missing member).
/// - <b>LandedCostDetailEntities emitted nil</b> — per-line duty/tax cannot be reconstructed
///   (request line items are dropped inbound; GLP returns totals only). The duties total is
///   preserved in RateEntity.LandedCost.
///
/// This is still the ONLY place for outbound element names and figure mapping.
/// </summary>
public static class OutboundFieldMap
{
    // ── Result children (PDSRouting namespace), in captured document order ──────
    public const string AccountNumber = "AccountNumber";
    public const string ResponseDateTime = "ResponseDateTime";
    public const string RateResponseEntries = "RateResponseEntries";
    public const string RateResponseEntry = "RateResponseEntry";
    public const string BoxId = "BoxID";
    public const string RateEntity = "RateEntity";
    public const string LandedCostDetailEntities = "LandedCostDetailEntities";
    public const string MessageEntities = "MessageEntities";
    public const string MessageEntity = "MessageEntity";
    public const string MessageCode = "Code";
    public const string MessageDescription = "Description";

    // ── business-level message emitted on success (captured verbatim) ───────────
    public const string SuccessCode = "00000";
    public const string SuccessDescription = "Request Successful";

    /// <summary>
    /// RateEntity children in captured document order. Null values are skipped by the builder
    /// (a missing member deserializes to its default on the WCF client side).
    /// </summary>
    public static IEnumerable<(string Tag, string? Value)> RateEntityElements(LandedCostResult r)
    {
        yield return ("ProcessingCost", "0");
        yield return ("BillableWeight", Num(r.BillableWeight));
        yield return ("BillableWeightUOM", r.BillableWeightUOM);
        // DimensionFactor deliberately omitted — see class remarks.
        yield return ("FreightCost", Num(r.TotalFreightCost ?? r.FreightCost));
        yield return ("InsureCost", "0");
        yield return ("LandedCost", Num(r.TotalTaxesDuties));
        yield return ("CurrencyCode", r.CurrencyCode);
    }

    private static string? Num(decimal? value) =>
        value?.ToString(CultureInfo.InvariantCulture);
}
