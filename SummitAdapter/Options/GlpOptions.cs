namespace SummitAdapter.Options;

/// <summary>
/// Binds the "Glp" section of appsettings.json (section 7). Endpoint paths live here, never in
/// handlers — operation routing is decided by the registry and turned into a path by config.
/// </summary>
public sealed class GlpOptions
{
    public const string SectionName = "Glp";

    /// <summary>Base URL of the Spring Boot GLP service, e.g. http://localhost:8080.</summary>
    public string BaseUrl { get; set; } = "http://localhost:8080";

    /// <summary>Rate endpoint path — public, read-only price check (rates, duties &amp; taxes),
    /// no DB write, no API key. Handled by GLP's RateCalculationController.</summary>
    public string RatePath { get; set; } = "/api/v1/shipping/rates";

    /// <summary>Ship endpoint path — creates the delivery; GLP writes tbl_consignee.
    /// Handled by GLP's ShipmentController, which returns 201 Created.</summary>
    public string ShipPath { get; set; } = "/api/v1/shipments";

    /// <summary>
    /// API key for the Ship endpoint only. Ship creates a delivery (DB write) and is protected;
    /// Rate is a public, keyless price check and must NOT receive this key. It is a deploy-time
    /// secret — leave it empty in source and supply it on the server via the Glp__ApiKey env var.
    /// </summary>
    public string ApiKey { get; set; } = "";
}
