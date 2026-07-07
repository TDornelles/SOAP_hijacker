namespace SummitAdapter.Models;

/// <summary>
/// The package DTO forwarded to GLP as JSON (camelCase). Field constraints are documented in
/// section 6 of the spec. The adapter only validates enough to fail fast with a clean Fault on
/// obviously malformed input; GLP performs authoritative validation.
/// </summary>
public sealed class PackageRequest
{
    /// <summary>Required, max 4 chars.</summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>Required, exactly 2 chars (ISO country code).</summary>
    public string DestinationCountryCode { get; set; } = string.Empty;

    /// <summary>Required, &gt; 0.</summary>
    public decimal Weight { get; set; }

    /// <summary>Optional, "Pounds" | "Kilograms" (default Pounds).</summary>
    public string WeightUOM { get; set; } = "Pounds";

    /// <summary>Required, &gt; 0.</summary>
    public decimal Length { get; set; }

    /// <summary>Required, &gt; 0.</summary>
    public decimal Width { get; set; }

    /// <summary>Required, &gt; 0.</summary>
    public decimal Height { get; set; }

    /// <summary>Optional, "Inches" | "Centimeters" (default Inches).</summary>
    public string DimensionUOM { get; set; } = "Inches";

    /// <summary>Required, &gt; 0.</summary>
    public decimal PackageValue { get; set; }
}
