namespace SummitAdapter.Soap;

public static class SoapConstants
{
    /// <summary>SOAP 1.1 envelope namespace.</summary>
    public const string SoapEnvelopeNamespace = "http://schemas.xmlsoap.org/soap/envelope/";

    /// <summary>Default operation namespace used by the legacy service.</summary>
    public const string TempuriNamespace = "http://tempuri.org/";

    /// <summary>Content type for every request and response (and every Fault), per section 3.</summary>
    public const string XmlContentType = "text/xml; charset=utf-8";
}
