using SummitAdapter.Soap;

namespace SummitAdapter.Dispatch;

/// <summary>
/// The single source of truth for how each SOAP operation is handled. Handlers contain no hardcoded
/// operation knowledge — they ask the registry. Lookups are case-insensitive on the operation name.
///
/// The adapter is a selective hijacker: it only translates the operations that have been ported to
/// GLP and forwards everything else to the legacy service untouched (see <see cref="OperationRouting"/>).
/// Each operation is listed on its own line with its own disposition so operations can be ported
/// <em>one at a time</em>: to bring an operation live, flip its line from <see cref="PassThrough"/>
/// to <see cref="Translate"/> and name its <see cref="GlpEndpoint"/>. An operation that is not
/// listed here is treated as pass-through too, so an un-enumerated SOAPAction never breaks Summit.
///
/// Current state: only the read-only Rate operations are ported. The Ship path (new package
/// insertions) is expected to be wired at go-live — flip <c>RouteDelivery…</c> to
/// <c>Translate(…, GlpEndpoint.Ship)</c> then. End goal: every line on Translate.
/// </summary>
public sealed class OperationRegistry
{
    private readonly IReadOnlyDictionary<string, OperationDescriptor> _operations;

    public OperationRegistry() : this(DefaultEntries())
    {
    }

    /// <summary>
    /// Test seam: build a registry with an explicit operation set (e.g. to exercise a ported Ship op
    /// before its production line is flipped). Production always uses the parameterless constructor,
    /// which is the single source of truth.
    /// </summary>
    internal OperationRegistry(IEnumerable<OperationDescriptor> entries)
    {
        _operations = entries.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static OperationDescriptor[] DefaultEntries()
    {
        return new[]
        {
            // ── Ported → translated to GLP ──────────────────────────────────────────────
            // Rate family: read-only price check, no DB write.
            Translate("Rate",           GlpEndpoint.Rate),
            Translate("RateLandedCost", GlpEndpoint.Rate),

            // ── Not yet ported → forwarded raw to the legacy service ────────────────────
            // New package insertions (Route/create) go live at cutover — flip these to
            // Translate(name, GlpEndpoint.Ship) as the Ship endpoint is wired.
            PassThrough("RouteDelivery"),
            PassThrough("RouteDeliveryRate"),
            PassThrough("RouteDeliveryRateLandedCost"),

            // Modifications, tracking, and end-of-day — not part of this rollout; stay on legacy.
            PassThrough("RouteUpdateDelivery"),
            PassThrough("RouteVoidDelivery"),
            PassThrough("GetTrackingHistory"),
            PassThrough("CloseEODProcess"),
        };
    }

    private static OperationDescriptor Translate(string name, GlpEndpoint endpoint) =>
        new(name, OperationRouting.Translate, endpoint, SoapConstants.TempuriNamespace);

    private static OperationDescriptor PassThrough(string name) =>
        new(name, OperationRouting.PassThrough, GlpEndpoint: null, SoapConstants.TempuriNamespace);

    public bool TryGet(string operationName, out OperationDescriptor descriptor)
    {
        if (operationName is not null && _operations.TryGetValue(operationName, out var found))
        {
            descriptor = found;
            return true;
        }

        descriptor = null!;
        return false;
    }
}
