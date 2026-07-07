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

    /// <summary>Rate endpoint path — read-only price check, no DB write.</summary>
    public string RatePath { get; set; } = "/api/rate";

    /// <summary>Ship endpoint path — creates the delivery; GLP writes tbl_consignee.</summary>
    public string ShipPath { get; set; } = "/api/ship";
}
