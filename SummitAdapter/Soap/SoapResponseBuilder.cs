using System.Globalization;
using System.Xml;
using SummitAdapter.Dispatch;
using SummitAdapter.Models;

namespace SummitAdapter.Soap;

/// <summary>
/// Builds the success SOAP 1.1 response envelope for a translated Rate-family operation
/// (section 5, step 7), reproducing the legacy service's captured shape
/// (fixtures/soap-rate-live-capture-2026-07-15.md):
/// <code>
/// &lt;soap:Envelope&gt;&lt;soap:Body&gt;
///   &lt;{Operation}Response xmlns="…"&gt;
///     &lt;{Operation}Result xmlns:a="…/PDSRouting" xmlns:i="…XMLSchema-instance"&gt;
///       …nested PDSRouting entries, see &lt;see cref="OutboundFieldMap"/&gt;…
///     &lt;/{Operation}Result&gt;
///   &lt;/{Operation}Response&gt;
/// &lt;/soap:Body&gt;&lt;/soap:Envelope&gt;
/// </code>
/// Element names and the GLP→legacy figure mapping live in <see cref="OutboundFieldMap"/>; the
/// request supplies the echoed fields (AccountNumber, BoxID). <c>XmlWriter</c> escapes all values.
/// </summary>
public sealed class SoapResponseBuilder
{
    private readonly TimeProvider _time;

    public SoapResponseBuilder(TimeProvider? time = null) => _time = time ?? TimeProvider.System;

    public string Build(OperationDescriptor descriptor, PackageRequest request, LandedCostResult result)
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

            // <{Operation}Result xmlns:a="…PDSRouting" xmlns:i="…XMLSchema-instance">
            writer.WriteStartElement($"{descriptor.Name}Result", descriptor.ResponseNamespace);
            writer.WriteAttributeString("xmlns", "a", null, SoapConstants.PdsRoutingNamespace);
            writer.WriteAttributeString("xmlns", "i", null, SoapConstants.XsiNamespace);

            WritePds(writer, OutboundFieldMap.AccountNumber, request.AccountNumber);
            WritePds(writer, OutboundFieldMap.ResponseDateTime,
                _time.GetUtcNow().ToString("o", CultureInfo.InvariantCulture));

            writer.WriteStartElement("a", OutboundFieldMap.RateResponseEntries, SoapConstants.PdsRoutingNamespace);
            writer.WriteStartElement("a", OutboundFieldMap.RateResponseEntry, SoapConstants.PdsRoutingNamespace);

            WritePds(writer, OutboundFieldMap.BoxId, request.BoxId);

            // <a:RateEntity> — the rate figures.
            writer.WriteStartElement("a", OutboundFieldMap.RateEntity, SoapConstants.PdsRoutingNamespace);
            foreach (var (tag, value) in OutboundFieldMap.RateEntityElements(result))
            {
                if (value is null)
                {
                    continue;
                }

                WritePds(writer, tag, value);
            }

            writer.WriteEndElement(); // RateEntity

            // <a:LandedCostDetailEntities i:nil="true"/> — see OutboundFieldMap remarks.
            writer.WriteStartElement("a", OutboundFieldMap.LandedCostDetailEntities, SoapConstants.PdsRoutingNamespace);
            writer.WriteAttributeString("nil", SoapConstants.XsiNamespace, "true");
            writer.WriteEndElement();

            // <a:MessageEntities><a:MessageEntity><a:Code>00000</a:Code>…
            writer.WriteStartElement("a", OutboundFieldMap.MessageEntities, SoapConstants.PdsRoutingNamespace);
            writer.WriteStartElement("a", OutboundFieldMap.MessageEntity, SoapConstants.PdsRoutingNamespace);
            WritePds(writer, OutboundFieldMap.MessageCode, OutboundFieldMap.SuccessCode);
            WritePds(writer, OutboundFieldMap.MessageDescription, OutboundFieldMap.SuccessDescription);
            writer.WriteEndElement(); // MessageEntity
            writer.WriteEndElement(); // MessageEntities

            writer.WriteEndElement(); // RateResponseEntry
            writer.WriteEndElement(); // RateResponseEntries

            writer.WriteEndElement(); // Result
            writer.WriteEndElement(); // Response
            writer.WriteEndElement(); // Body
            writer.WriteEndElement(); // Envelope
            writer.WriteEndDocument();
        }

        return stringWriter.ToString();
    }

    private static void WritePds(XmlWriter writer, string tag, string value)
    {
        writer.WriteStartElement("a", tag, SoapConstants.PdsRoutingNamespace);
        writer.WriteString(value);
        writer.WriteEndElement();
    }
}
