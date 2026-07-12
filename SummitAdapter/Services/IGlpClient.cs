using SummitAdapter.Dispatch;
using SummitAdapter.Models;

namespace SummitAdapter.Services;

/// <summary>GLP's raw HTTP response, relayed back to the caller unchanged (Ship path).</summary>
/// <param name="StatusCode">HTTP status GLP returned (e.g. 201 Created).</param>
/// <param name="ContentType">Content type GLP returned (typically application/json).</param>
/// <param name="Body">Raw response body.</param>
public sealed record GlpRawResponse(int StatusCode, string ContentType, string Body);

/// <summary>Typed client over the GLP Spring Boot service.</summary>
public interface IGlpClient
{
    /// <summary>
    /// Forward the package to the GLP endpoint chosen by <paramref name="endpoint"/>
    /// (<see cref="GlpEndpoint.Rate"/> → RatePath, <see cref="GlpEndpoint.Ship"/> → ShipPath) and
    /// return the landed-cost figures. Throws <see cref="GlpException"/> on a non-success response.
    /// Used by the Rate family, whose response is translated into a typed SOAP envelope.
    /// </summary>
    Task<LandedCostResult> SendAsync(GlpEndpoint endpoint, PackageRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Forward the package to GLP and return its raw HTTP response (status, content type, body)
    /// unchanged, so the handler can relay exactly what GLP returned. Used by the Ship path, whose
    /// response shape is GLP's to define. Unlike <see cref="SendAsync"/> this does NOT throw on a
    /// non-success status — an error status/body from GLP is relayed too; only a transport failure
    /// (GLP unreachable) throws <see cref="GlpException"/>.
    /// </summary>
    Task<GlpRawResponse> SendRawAsync(GlpEndpoint endpoint, PackageRequest request, CancellationToken cancellationToken);
}
