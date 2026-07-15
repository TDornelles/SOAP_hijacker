using System.Globalization;
using System.Xml.Linq;
using SummitAdapter.Models;

namespace SummitAdapter.Soap;

/// <summary>
/// Parses <c>&lt;rData&gt;</c> into a <see cref="PackageRequest"/> (section 5, step 4). The real
/// Summit shape (confirmed by capture, see <see cref="InboundFieldMap"/>) is nested: account and
/// destination sit directly under <c>rData</c>; the dimensions/value sit inside
/// <c>RatePackageRequests &gt; RatePackageRequest</c>. GLP rates one package per call, so exactly
/// one package element is required — a multi-package request is rejected with a clean Fault rather
/// than silently rating only the first box. Captured fields GLP does not accept (WSKEY, dates,
/// insurance, line items, …) are ignored. Lookups are namespace-agnostic and case-insensitive.
/// Only enough validation is performed to fail fast — GLP does authoritative validation.
/// </summary>
public sealed class SoapRequestParser
{
    private static readonly string[] WeightUoms = { "Pounds", "Kilograms" };
    private static readonly string[] DimensionUoms = { "Inches", "Centimeters" };

    public PackageRequest Parse(string body)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(body);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new SoapParseException("Request body is not well-formed XML.", ex);
        }

        var rData = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "rData")
            ?? throw new SoapParseException("Request is missing the <rData> element.");

        var header = IndexChildren(rData);

        // Locate the single package. Zero packages is a malformed request; more than one cannot be
        // priced by GLP's one-package rate call, and rating only the first would return a wrong
        // (too-low) price — so both are Client Faults.
        var packages = rData.Descendants()
            .Where(e => e.Name.LocalName.Equals(InboundFieldMap.Package, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (packages.Count == 0)
        {
            throw new SoapParseException(
                $"Request contains no <{InboundFieldMap.Package}> element under <rData>.");
        }

        if (packages.Count > 1)
        {
            throw new SoapParseException(
                $"Request contains {packages.Count} <{InboundFieldMap.Package}> elements; " +
                "only single-package rate requests are supported.");
        }

        var package = IndexChildren(packages[0]);

        var request = new PackageRequest
        {
            AccountNumber = RequiredString(header, InboundFieldMap.AccountNumber, maxLength: 4),
            DestinationCountryCode = RequiredString(header, InboundFieldMap.DestinationCountryCode, exactLength: 2),
            Weight = RequiredPositiveNumber(package, InboundFieldMap.Weight),
            WeightUOM = OptionalEnum(package, InboundFieldMap.WeightUOM, WeightUoms, "Pounds"),
            Length = RequiredPositiveNumber(package, InboundFieldMap.Length),
            Width = RequiredPositiveNumber(package, InboundFieldMap.Width),
            Height = RequiredPositiveNumber(package, InboundFieldMap.Height),
            DimensionUOM = OptionalEnum(package, InboundFieldMap.DimensionUOM, DimensionUoms, "Inches"),
            PackageValue = RequiredPositiveNumber(package, InboundFieldMap.PackageValue),
            // Echo-only (never forwarded to GLP): the response repeats the request's BoxID.
            BoxId = package.TryGetValue(InboundFieldMap.BoxId, out var boxId)
                    && !string.IsNullOrEmpty(boxId) ? boxId : "0",
        };

        return request;
    }

    /// <summary>Index an element's children by local name (namespace-agnostic, case-insensitive).
    /// First wins on duplicates.</summary>
    private static Dictionary<string, string> IndexChildren(XElement parent)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in parent.Elements())
        {
            var key = child.Name.LocalName;
            if (!fields.ContainsKey(key))
            {
                fields[key] = (child.Value ?? string.Empty).Trim();
            }
        }

        return fields;
    }

    private static string RequiredString(
        IReadOnlyDictionary<string, string> fields, string name, int maxLength = int.MaxValue, int exactLength = -1)
    {
        if (!fields.TryGetValue(name, out var value) || string.IsNullOrEmpty(value))
        {
            throw new SoapParseException($"Required field '{name}' is missing or empty.");
        }

        if (exactLength >= 0 && value.Length != exactLength)
        {
            throw new SoapParseException($"Field '{name}' must be exactly {exactLength} characters.");
        }

        if (value.Length > maxLength)
        {
            throw new SoapParseException($"Field '{name}' must be at most {maxLength} characters.");
        }

        return value;
    }

    private static decimal RequiredPositiveNumber(IReadOnlyDictionary<string, string> fields, string name)
    {
        if (!fields.TryGetValue(name, out var value) || string.IsNullOrEmpty(value))
        {
            throw new SoapParseException($"Required field '{name}' is missing or empty.");
        }

        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            throw new SoapParseException($"Field '{name}' is not a valid number: '{value}'.");
        }

        if (number <= 0)
        {
            throw new SoapParseException($"Field '{name}' must be greater than 0.");
        }

        return number;
    }

    private static string OptionalEnum(
        IReadOnlyDictionary<string, string> fields, string name, string[] allowed, string defaultValue)
    {
        if (!fields.TryGetValue(name, out var value) || string.IsNullOrEmpty(value))
        {
            return defaultValue;
        }

        var match = allowed.FirstOrDefault(a => a.Equals(value, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            throw new SoapParseException(
                $"Field '{name}' must be one of [{string.Join(", ", allowed)}] but was '{value}'.");
        }

        return match;
    }
}
