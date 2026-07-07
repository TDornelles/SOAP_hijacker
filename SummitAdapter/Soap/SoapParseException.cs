namespace SummitAdapter.Soap;

/// <summary>
/// Thrown when the inbound SOAP/XML cannot be turned into a valid <see cref="Models.PackageRequest"/>
/// (malformed XML, missing required fields, bad numbers). Surfaced to Summit as a SOAP 1.1
/// <c>Client</c> Fault.
/// </summary>
public sealed class SoapParseException : Exception
{
    public SoapParseException(string message) : base(message)
    {
    }

    public SoapParseException(string message, Exception inner) : base(message, inner)
    {
    }
}
