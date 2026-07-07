namespace SummitAdapter.Services;

/// <summary>The verbatim response from the legacy service, relayed back to Summit unchanged.</summary>
/// <param name="StatusCode">HTTP status returned by the legacy service.</param>
/// <param name="ContentType">Content type returned by the legacy service (SOAP text/xml).</param>
/// <param name="Body">Raw response body.</param>
public sealed record LegacyResponse(int StatusCode, string ContentType, string Body);

/// <summary>
/// Forwards an un-ported SOAP operation to the relocated legacy service and relays its response
/// verbatim. The adapter does not parse or translate pass-through traffic — Summit sees exactly what
/// the legacy service would have returned.
/// </summary>
public interface ILegacyForwarder
{
    /// <summary>
    /// POST the original request to the legacy service at <paramref name="path"/> (the same
    /// <c>.svc</c> path Summit called), preserving the SOAPAction header and content type, and
    /// return its raw response. Throws <see cref="LegacyForwardException"/> if the legacy service
    /// cannot be reached.
    /// </summary>
    Task<LegacyResponse> ForwardAsync(
        string path, string soapAction, string contentType, string body, CancellationToken cancellationToken);
}
