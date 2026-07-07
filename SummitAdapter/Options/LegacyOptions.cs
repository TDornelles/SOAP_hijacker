namespace SummitAdapter.Options;

/// <summary>
/// Binds the "Legacy" section of appsettings.json. Points at the relocated origin where the real
/// WCF Routing.svc now lives, so the adapter can forward operations it hasn't ported yet. This must
/// be a <em>different</em> address from the public host the adapter sits on (e.g. an internal name),
/// otherwise pass-through would loop back into the adapter.
/// </summary>
public sealed class LegacyOptions
{
    public const string SectionName = "Legacy";

    /// <summary>Base URL of the relocated legacy service, reachable FROM the adapter's server,
    /// e.g. http://pds-origin.internal.</summary>
    public string BaseUrl { get; set; } = "http://localhost:9090";
}
