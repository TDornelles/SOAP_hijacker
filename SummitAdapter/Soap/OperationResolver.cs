using System.Xml.Linq;

namespace SummitAdapter.Soap;

/// <summary>
/// Resolves the operation name for a request (section 5, step 2): prefer the SOAPAction header
/// (strip quotes, take the segment after the last '/'); fall back to the local name of the first
/// child of <c>soap:Body</c> when the header is absent or empty.
/// </summary>
public static class OperationResolver
{
    public static string Resolve(string? soapAction, string body)
    {
        var fromAction = FromSoapAction(soapAction);
        if (!string.IsNullOrEmpty(fromAction))
        {
            return fromAction;
        }

        var fromBody = FromBody(body);
        if (!string.IsNullOrEmpty(fromBody))
        {
            return fromBody;
        }

        throw new SoapParseException(
            "Could not determine the operation: no usable SOAPAction header and no element under soap:Body.");
    }

    /// <summary>
    /// e.g. <c>"http://tempuri.org/IRouting/RateLandedCost"</c> → <c>RateLandedCost</c>.
    /// Returns null when the header is missing or empty after trimming quotes.
    /// </summary>
    public static string? FromSoapAction(string? soapAction)
    {
        if (string.IsNullOrWhiteSpace(soapAction))
        {
            return null;
        }

        var value = soapAction.Trim().Trim('"').Trim();
        if (value.Length == 0)
        {
            return null;
        }

        var lastSlash = value.LastIndexOf('/');
        var segment = lastSlash >= 0 ? value[(lastSlash + 1)..] : value;
        return segment.Length == 0 ? null : segment;
    }

    /// <summary>Local name of the first element child of soap:Body. Returns null if not found.</summary>
    public static string? FromBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        XDocument doc;
        try
        {
            doc = XDocument.Parse(body);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new SoapParseException("Request body is not well-formed XML.", ex);
        }

        var bodyElement = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Body"
                                 && e.Name.NamespaceName == SoapConstants.SoapEnvelopeNamespace)
            ?? doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Body");

        var operation = bodyElement?.Elements().FirstOrDefault();
        return operation?.Name.LocalName;
    }
}
