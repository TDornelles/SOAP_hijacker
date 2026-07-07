namespace SummitAdapter.Tests.TestSupport;

/// <summary>Loads the placeholder XML fixtures copied next to the test binaries.</summary>
public static class Fixtures
{
    public static string Load(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", name);
        return File.ReadAllText(path);
    }

    public const string RateRequest = "rate-request.xml";
    public const string RouteRequest = "route-request.xml";
}
