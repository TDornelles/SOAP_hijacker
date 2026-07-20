namespace SummitAdapter.Services;

/// <summary>
/// One audited call. Assembled across the request — <see cref="SummitAdapter.Endpoints.SoapHandler"/>
/// fills the SOAP-level fields; <see cref="GlpClient"/> fills the GLP leg via the shared instance on
/// <c>HttpContext.Items[<see cref="ItemsKey"/>]</c> — then written as a single JSON line by
/// <see cref="ICallAudit"/>. Mutable by design.
/// </summary>
public sealed class CallAuditRecord
{
    /// <summary>Key under which the in-flight record lives on <c>HttpContext.Items</c>.</summary>
    public const string ItemsKey = "__callAudit";

    public DateTimeOffset Ts { get; set; } = DateTimeOffset.UtcNow;
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string? Op { get; set; }

    /// <summary><c>translate</c>, <c>passthrough</c>, or null if a fault preceded the decision.</summary>
    public string? Route { get; set; }

    public string? Account { get; set; }
    public string? DestCountry { get; set; }

    /// <summary>Inbound SOAP, WSKEY redacted.</summary>
    public string? SoapIn { get; set; }

    /// <summary>JSON POSTed to GLP (embedded object when valid JSON).</summary>
    public object? GlpReq { get; set; }

    /// <summary>Raw GLP response body (embedded object when valid JSON).</summary>
    public object? GlpResp { get; set; }

    public int? GlpStatus { get; set; }

    /// <summary>Response returned to the caller (translated SOAP, relayed legacy body, or fault).</summary>
    public string? SoapOut { get; set; }

    public int Status { get; set; }
    public long Ms { get; set; }
    public string? Error { get; set; }
}
