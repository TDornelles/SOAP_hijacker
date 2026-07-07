using System.Xml;

namespace SummitAdapter.Soap;

/// <summary>SOAP 1.1 fault codes used by the adapter.</summary>
public enum SoapFaultCode
{
    /// <summary>Caller's fault — malformed XML, unknown operation, bad/missing fields.</summary>
    Client,

    /// <summary>Our/downstream fault — GLP error, unwired operation.</summary>
    Server
}

/// <summary>
/// Builds SOAP 1.1 Faults (section 5 / guardrails). Every error path returns one of these with
/// content type <c>text/xml; charset=utf-8</c> — never a bare HTTP error page. The fault string is
/// written via <c>XmlWriter</c> so special characters are escaped.
/// </summary>
public sealed class SoapFaultBuilder
{
    public string Build(SoapFaultCode code, string message)
    {
        using var stringWriter = new Utf8StringWriter();
        var settings = new XmlWriterSettings
        {
            Indent = false,
            OmitXmlDeclaration = false,
        };

        using (var writer = XmlWriter.Create(stringWriter, settings))
        {
            writer.WriteStartDocument();

            writer.WriteStartElement("soap", "Envelope", SoapConstants.SoapEnvelopeNamespace);
            writer.WriteStartElement("soap", "Body", SoapConstants.SoapEnvelopeNamespace);
            writer.WriteStartElement("soap", "Fault", SoapConstants.SoapEnvelopeNamespace);

            // faultcode/faultstring are unqualified in SOAP 1.1; the code value is the soap-prefixed enum.
            writer.WriteStartElement("faultcode");
            writer.WriteString($"soap:{code}");
            writer.WriteEndElement();

            writer.WriteStartElement("faultstring");
            writer.WriteString(message);
            writer.WriteEndElement();

            writer.WriteEndElement(); // Fault
            writer.WriteEndElement(); // Body
            writer.WriteEndElement(); // Envelope
            writer.WriteEndDocument();
        }

        return stringWriter.ToString();
    }
}
