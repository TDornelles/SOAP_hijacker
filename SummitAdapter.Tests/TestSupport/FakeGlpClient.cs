using SummitAdapter.Dispatch;
using SummitAdapter.Models;
using SummitAdapter.Services;

namespace SummitAdapter.Tests.TestSupport;

/// <summary>
/// In-memory <see cref="IGlpClient"/> for tests. Records every call (so routing can be asserted)
/// and returns a canned result, or throws a supplied exception.
/// </summary>
public sealed class FakeGlpClient : IGlpClient
{
    private readonly Func<GlpEndpoint, PackageRequest, LandedCostResult> _responder;

    public FakeGlpClient(LandedCostResult? result = null)
    {
        var canned = result ?? Default();
        _responder = (_, _) => canned;
    }

    public FakeGlpClient(Func<GlpEndpoint, PackageRequest, LandedCostResult> responder)
    {
        _responder = responder;
    }

    public List<(GlpEndpoint Endpoint, PackageRequest Request)> Calls { get; } = new();

    public GlpEndpoint? LastEndpoint => Calls.Count > 0 ? Calls[^1].Endpoint : null;

    public Task<LandedCostResult> SendAsync(
        GlpEndpoint endpoint, PackageRequest request, CancellationToken cancellationToken)
    {
        Calls.Add((endpoint, request));
        return Task.FromResult(_responder(endpoint, request));
    }

    public static LandedCostResult Default() => new()
    {
        FreightCost = 42.50m,
        FuelSurcharge = 5.10m,
        TotalFreightCost = 47.60m,
        DutyValue = 12.00m,
        VatValue = 9.40m,
        TotalTaxesDuties = 21.40m,
        TotalCost = 69.00m,
        CurrencyCode = "USD",
        BillableWeight = 13.0m,
        BillableWeightUOM = "Pounds",
        DimensionalWeight = 11.1m
    };
}
