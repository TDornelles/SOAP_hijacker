using SummitAdapter.Soap;
using SummitAdapter.Tests.TestSupport;
using Xunit;

namespace SummitAdapter.Tests;

/// <summary>
/// Parser tests against the REAL Summit shape (confirmed by capture): header fields directly under
/// rData, package fields inside RatePackageRequests > RatePackageRequest. Extra captured fields
/// (WSKEY, dates, insurance, line items) are ignored; exactly one package is required.
/// </summary>
public class SoapRequestParserTests
{
    private readonly SoapRequestParser _parser = new();

    /// <summary>Nested rData body builder: header + one package, with substitutable pieces.</summary>
    private static string Body(
        string account = "<AccountNumber>AB</AccountNumber>",
        string country = "<DestinationCountryCode>US</DestinationCountryCode>",
        string packageFields = """
            <Weight>1</Weight>
            <Length>1</Length><Width>1</Width><Height>1</Height>
            <PackageValue>1</PackageValue>
            """) => $"""
        <Op><rData>
          {account}
          {country}
          <RatePackageRequests>
            <RatePackageRequest>
              {packageFields}
            </RatePackageRequest>
          </RatePackageRequests>
        </rData></Op>
        """;

    [Fact]
    public void Parses_real_captured_request_into_dto()
    {
        // fixtures/rate-request.xml is the real capture (WSKEY redacted) — the authoritative test.
        var request = _parser.Parse(Fixtures.Load(Fixtures.RateRequest));

        Assert.Equal("3528", request.AccountNumber);
        Assert.Equal("NZ", request.DestinationCountryCode);
        Assert.Equal(0.45m, request.Weight);
        Assert.Equal("Pounds", request.WeightUOM);
        Assert.Equal(11m, request.Length);
        Assert.Equal(7m, request.Width);
        Assert.Equal(2m, request.Height);
        Assert.Equal("Inches", request.DimensionUOM);
        Assert.Equal(12.99m, request.PackageValue);
    }

    [Fact]
    public void Optional_uoms_default_when_absent()
    {
        var request = _parser.Parse(Body());

        Assert.Equal("Pounds", request.WeightUOM);
        Assert.Equal("Inches", request.DimensionUOM);
    }

    [Fact]
    public void Lookup_is_namespace_agnostic()
    {
        // The real capture uses the d4p1 datacontract namespace; any namespace must work.
        const string body = """
            <s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
              <s:Body>
                <Op xmlns="http://tempuri.org/">
                  <rData xmlns:x="urn:whatever">
                    <x:AccountNumber>AB</x:AccountNumber>
                    <x:DestinationCountryCode>US</x:DestinationCountryCode>
                    <x:RatePackageRequests>
                      <x:RatePackageRequest>
                        <x:Weight>2</x:Weight>
                        <x:Length>1</x:Length><x:Width>1</x:Width><x:Height>1</x:Height>
                        <x:PackageValue>5</x:PackageValue>
                      </x:RatePackageRequest>
                    </x:RatePackageRequests>
                  </rData>
                </Op>
              </s:Body>
            </s:Envelope>
            """;

        var request = _parser.Parse(body);

        Assert.Equal("AB", request.AccountNumber);
        Assert.Equal(2m, request.Weight);
    }

    [Fact]
    public void Unknown_captured_fields_are_ignored()
    {
        // WSKEY, dates, insurance, line items etc. must not break parsing — GLP never sees them.
        const string body = """
            <Op><rData>
              <WSKEY>00000000-0000-0000-0000-000000000000</WSKEY>
              <AccountNumber>AB</AccountNumber>
              <RequestDateTime>2025-05-19T10:38:22-04:00</RequestDateTime>
              <SourceOfRequest>WEBSERVICE</SourceOfRequest>
              <DestinationCountryCode>US</DestinationCountryCode>
              <DestinationPostalCode>4122</DestinationPostalCode>
              <RatePackageRequests>
                <RatePackageRequest>
                  <BoxID>0</BoxID>
                  <Weight>1</Weight>
                  <Length>1</Length><Width>1</Width><Height>1</Height>
                  <Insure>false</Insure>
                  <PackageValue>1</PackageValue>
                  <CurrencyCode>USD</CurrencyCode>
                  <RatePackageDetailRequests>
                    <RatePackageDetailRequest>
                      <HarmonizedCode>8205513060</HarmonizedCode>
                      <Quantity>1</Quantity>
                    </RatePackageDetailRequest>
                  </RatePackageDetailRequests>
                </RatePackageRequest>
              </RatePackageRequests>
            </rData></Op>
            """;

        var request = _parser.Parse(body);

        Assert.Equal("AB", request.AccountNumber);
        Assert.Equal(1m, request.Weight);
    }

    [Fact]
    public void Missing_package_element_throws()
    {
        const string body = """
            <Op><rData>
              <AccountNumber>AB</AccountNumber>
              <DestinationCountryCode>US</DestinationCountryCode>
            </rData></Op>
            """;

        var ex = Assert.Throws<SoapParseException>(() => _parser.Parse(body));
        Assert.Contains("RatePackageRequest", ex.Message);
    }

    [Fact]
    public void Multiple_packages_throw_rather_than_rating_only_the_first()
    {
        const string body = """
            <Op><rData>
              <AccountNumber>AB</AccountNumber>
              <DestinationCountryCode>US</DestinationCountryCode>
              <RatePackageRequests>
                <RatePackageRequest>
                  <Weight>1</Weight><Length>1</Length><Width>1</Width><Height>1</Height>
                  <PackageValue>1</PackageValue>
                </RatePackageRequest>
                <RatePackageRequest>
                  <Weight>2</Weight><Length>2</Length><Width>2</Width><Height>2</Height>
                  <PackageValue>2</PackageValue>
                </RatePackageRequest>
              </RatePackageRequests>
            </rData></Op>
            """;

        var ex = Assert.Throws<SoapParseException>(() => _parser.Parse(body));
        Assert.Contains("single-package", ex.Message);
    }

    [Fact]
    public void Malformed_xml_throws_parse_exception()
    {
        Assert.Throws<SoapParseException>(() => _parser.Parse("<not-closed>"));
    }

    [Fact]
    public void Missing_required_header_field_throws()
    {
        var ex = Assert.Throws<SoapParseException>(() => _parser.Parse(Body(account: "")));
        Assert.Contains("AccountNumber", ex.Message);
    }

    [Fact]
    public void Non_numeric_weight_throws()
    {
        var body = Body(packageFields: """
            <Weight>heavy</Weight>
            <Length>1</Length><Width>1</Width><Height>1</Height>
            <PackageValue>1</PackageValue>
            """);

        Assert.Throws<SoapParseException>(() => _parser.Parse(body));
    }

    [Fact]
    public void Non_positive_number_throws()
    {
        var body = Body(packageFields: """
            <Weight>0</Weight>
            <Length>1</Length><Width>1</Width><Height>1</Height>
            <PackageValue>1</PackageValue>
            """);

        Assert.Throws<SoapParseException>(() => _parser.Parse(body));
    }

    [Fact]
    public void Account_number_over_four_chars_throws()
    {
        Assert.Throws<SoapParseException>(() =>
            _parser.Parse(Body(account: "<AccountNumber>TOOLONG</AccountNumber>")));
    }

    [Fact]
    public void Country_code_not_two_chars_throws()
    {
        Assert.Throws<SoapParseException>(() =>
            _parser.Parse(Body(country: "<DestinationCountryCode>USA</DestinationCountryCode>")));
    }

    [Fact]
    public void Invalid_uom_throws()
    {
        var body = Body(packageFields: """
            <Weight>1</Weight>
            <WeightUOM>Stones</WeightUOM>
            <Length>1</Length><Width>1</Width><Height>1</Height>
            <PackageValue>1</PackageValue>
            """);

        Assert.Throws<SoapParseException>(() => _parser.Parse(body));
    }
}
