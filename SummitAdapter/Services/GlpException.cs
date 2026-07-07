namespace SummitAdapter.Services;

/// <summary>
/// Thrown when GLP returns a non-success status or an unusable body. Surfaced to Summit as a
/// SOAP 1.1 <c>Server</c> Fault; the status and body are logged (section 5, step 6).
/// </summary>
public sealed class GlpException : Exception
{
    public int StatusCode { get; }
    public string? ResponseBody { get; }

    public GlpException(string message, int statusCode = 0, string? responseBody = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
