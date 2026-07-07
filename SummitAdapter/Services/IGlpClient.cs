using SummitAdapter.Dispatch;
using SummitAdapter.Models;

namespace SummitAdapter.Services;

/// <summary>Typed client over the GLP Spring Boot service.</summary>
public interface IGlpClient
{
    /// <summary>
    /// Forward the package to the GLP endpoint chosen by <paramref name="endpoint"/>
    /// (<see cref="GlpEndpoint.Rate"/> → RatePath, <see cref="GlpEndpoint.Ship"/> → ShipPath) and
    /// return the landed-cost figures. Throws <see cref="GlpException"/> on a non-success response.
    /// </summary>
    Task<LandedCostResult> SendAsync(GlpEndpoint endpoint, PackageRequest request, CancellationToken cancellationToken);
}
