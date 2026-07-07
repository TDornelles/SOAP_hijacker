using SummitAdapter.Services;

namespace SummitAdapter.Tests.TestSupport;

/// <summary>
/// In-memory <see cref="ILegacyForwarder"/> for tests. Records every forwarded call (so pass-through
/// routing can be asserted) and returns a canned legacy response, or throws to simulate the legacy
/// service being unreachable.
/// </summary>
public sealed class FakeLegacyForwarder : ILegacyForwarder
{
    private readonly LegacyResponse _canned;
    private readonly bool _throw;

    public FakeLegacyForwarder(LegacyResponse? canned = null, bool @throw = false)
    {
        _canned = canned ?? new LegacyResponse(200, "text/xml; charset=utf-8", "<legacy>relayed</legacy>");
        _throw = @throw;
    }

    public List<(string Path, string SoapAction, string ContentType, string Body)> Calls { get; } = new();

    public Task<LegacyResponse> ForwardAsync(
        string path, string soapAction, string contentType, string body, CancellationToken cancellationToken)
    {
        Calls.Add((path, soapAction, contentType, body));
        if (_throw)
        {
            throw new LegacyForwardException("Simulated legacy outage.");
        }
        return Task.FromResult(_canned);
    }
}
