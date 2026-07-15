namespace SummitAdapter.Soap;

public static class SoapConstants
{
    /// <summary>SOAP 1.1 envelope namespace.</summary>
    public const string SoapEnvelopeNamespace = "http://schemas.xmlsoap.org/soap/envelope/";

    /// <summary>Default operation namespace used by the legacy service.</summary>
    public const string TempuriNamespace = "http://tempuri.org/";

    /// <summary>Datacontract namespace of the legacy PDSRouting types — the rData request fields
    /// and the Result response entries both live here (confirmed by live capture 2026-07-15).</summary>
    public const string PdsRoutingNamespace = "http://schemas.datacontract.org/2004/07/PDSRouting";

    /// <summary>XML Schema instance namespace (for <c>i:nil</c> markers, as legacy emits them).</summary>
    public const string XsiNamespace = "http://www.w3.org/2001/XMLSchema-instance";

    /// <summary>Content type for every request and response (and every Fault), per section 3.</summary>
    public const string XmlContentType = "text/xml; charset=utf-8";
}
