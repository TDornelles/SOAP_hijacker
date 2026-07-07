namespace SummitAdapter.Services;

/// <summary>
/// Thrown when the legacy service cannot be reached for a pass-through operation. Surfaced to Summit
/// as a SOAP 1.1 <c>Server</c> Fault; the cause is logged.
/// </summary>
public sealed class LegacyForwardException : Exception
{
    public LegacyForwardException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}
