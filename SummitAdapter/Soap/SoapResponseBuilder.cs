using System.Xml;
using SummitAdapter.Dispatch;
using SummitAdapter.Models;

namespace SummitAdapter.Soap;

/// <summary>
/// Builds the success SOAP 1.1 response envelope for an operation (section 5, step 7):
/// <code>
/// &lt;soap:Envelope&gt;&lt;soap:Body&gt;
///   &lt;{Operation}Response xmlns="..."&gt;
///     &lt;{Operation}Result&gt; ...landed-cost fields... &lt;/{Operation}Result&gt;
///   &lt;/{Operation}Response&gt;
/// &lt;/soap:Body&gt;&lt;/soap:Envelope&gt;
/// </code>
/// Result element names come from the stubbed <see cref="OutboundFieldMap"/>. <c>XmlWriter</c>
/// escapes all values (XML special characters are handled for free).
/// </summary>
public sealed class SoapResponseBuilder
{
    public string Build(OperationDescriptor descriptor, LandedCostResult result)
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

            // <{Operation}Response xmlns="<responseNamespace>">
            writer.WriteStartElement($"{descriptor.Name}Response", descriptor.ResponseNamespace);

            // <{Operation}Result> ... </{Operation}Result>
            writer.WriteStartElement($"{descriptor.Name}Result", descriptor.ResponseNamespace);
            foreach (var (tag, value) in OutboundFieldMap.Elements(result))
            {
                if (value is null)
                {
                    continue;
                }

                writer.WriteStartElement(tag, descriptor.ResponseNamespace);
                writer.WriteString(value);
                writer.WriteEndElement();
            }

            writer.WriteEndElement(); // Result
            writer.WriteEndElement(); // Response
            writer.WriteEndElement(); // Body
            writer.WriteEndElement(); // Envelope
            writer.WriteEndDocument();
        }

        return stringWriter.ToString();
    }
}
