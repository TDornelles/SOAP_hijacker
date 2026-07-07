using System.Globalization;
using System.Xml.Linq;
using SummitAdapter.Models;

namespace SummitAdapter.Soap;

/// <summary>
/// Parses <c>&lt;rData&gt;</c> into a <see cref="PackageRequest"/> (section 5, step 4). Element
/// names come from the stubbed <see cref="InboundFieldMap"/>; lookups are namespace-agnostic and
/// case-insensitive. Only enough validation is performed to fail fast with a clean Fault on
/// obviously malformed input — GLP does authoritative validation.
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

        // Index children by local name (namespace-agnostic, case-insensitive). First wins on dupes.
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in rData.Elements())
        {
            var key = child.Name.LocalName;
            if (!fields.ContainsKey(key))
            {
                fields[key] = (child.Value ?? string.Empty).Trim();
            }
        }

        var request = new PackageRequest
        {
            AccountNumber = RequiredString(fields, InboundFieldMap.AccountNumber, maxLength: 4),
            DestinationCountryCode = RequiredString(fields, InboundFieldMap.DestinationCountryCode, exactLength: 2),
            Weight = RequiredPositiveNumber(fields, InboundFieldMap.Weight),
            WeightUOM = OptionalEnum(fields, InboundFieldMap.WeightUOM, WeightUoms, "Pounds"),
            Length = RequiredPositiveNumber(fields, InboundFieldMap.Length),
            Width = RequiredPositiveNumber(fields, InboundFieldMap.Width),
            Height = RequiredPositiveNumber(fields, InboundFieldMap.Height),
            DimensionUOM = OptionalEnum(fields, InboundFieldMap.DimensionUOM, DimensionUoms, "Inches"),
            PackageValue = RequiredPositiveNumber(fields, InboundFieldMap.PackageValue),
        };

        return request;
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
