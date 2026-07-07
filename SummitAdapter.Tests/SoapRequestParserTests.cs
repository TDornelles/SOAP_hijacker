using SummitAdapter.Soap;
using SummitAdapter.Tests.TestSupport;
using Xunit;

namespace SummitAdapter.Tests;

public class SoapRequestParserTests
{
    private readonly SoapRequestParser _parser = new();

    [Fact]
    public void Parses_populated_rData_into_dto()
    {
        var request = _parser.Parse(Fixtures.Load(Fixtures.RateRequest));

        Assert.Equal("ACME", request.AccountNumber);
        Assert.Equal("CA", request.DestinationCountryCode);
        Assert.Equal(12.5m, request.Weight);
        Assert.Equal("Pounds", request.WeightUOM);
        Assert.Equal(10m, request.Length);
        Assert.Equal(8m, request.Width);
        Assert.Equal(6m, request.Height);
        Assert.Equal("Inches", request.DimensionUOM);
        Assert.Equal(250.00m, request.PackageValue);
    }

    [Fact]
    public void Optional_uoms_default_when_absent()
    {
        const string body = """
            <Envelope><Body><Op><rData>
              <accountNumber>AB</accountNumber>
              <destinationCountryCode>US</destinationCountryCode>
              <weight>1</weight>
              <length>1</length><width>1</width><height>1</height>
              <packageValue>1</packageValue>
            </rData></Op></Body></Envelope>
            """;

        var request = _parser.Parse(body);

        Assert.Equal("Pounds", request.WeightUOM);
        Assert.Equal("Inches", request.DimensionUOM);
    }

    [Fact]
    public void Lookup_is_namespace_agnostic()
    {
        const string body = """
            <s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
              <s:Body>
                <Op xmlns="http://tempuri.org/">
                  <rData xmlns="urn:whatever">
                    <accountNumber>AB</accountNumber>
                    <destinationCountryCode>US</destinationCountryCode>
                    <weight>2</weight>
                    <length>1</length><width>1</width><height>1</height>
                    <packageValue>5</packageValue>
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
    public void Malformed_xml_throws_parse_exception()
    {
        Assert.Throws<SoapParseException>(() => _parser.Parse("<not-closed>"));
    }

    [Fact]
    public void Missing_required_field_throws()
    {
        const string body = "<Op><rData><weight>1</weight></rData></Op>";
        Assert.Throws<SoapParseException>(() => _parser.Parse(body));
    }

    [Fact]
    public void Non_numeric_weight_throws()
    {
        const string body = """
            <Op><rData>
              <accountNumber>AB</accountNumber>
              <destinationCountryCode>US</destinationCountryCode>
              <weight>heavy</weight>
              <length>1</length><width>1</width><height>1</height>
              <packageValue>1</packageValue>
            </rData></Op>
            """;
        Assert.Throws<SoapParseException>(() => _parser.Parse(body));
    }

    [Fact]
    public void Non_positive_number_throws()
    {
        const string body = """
            <Op><rData>
              <accountNumber>AB</accountNumber>
              <destinationCountryCode>US</destinationCountryCode>
              <weight>0</weight>
              <length>1</length><width>1</width><height>1</height>
              <packageValue>1</packageValue>
            </rData></Op>
            """;
        Assert.Throws<SoapParseException>(() => _parser.Parse(body));
    }

    [Fact]
    public void Account_number_over_four_chars_throws()
    {
        const string body = """
            <Op><rData>
              <accountNumber>TOOLONG</accountNumber>
              <destinationCountryCode>US</destinationCountryCode>
              <weight>1</weight>
              <length>1</length><width>1</width><height>1</height>
              <packageValue>1</packageValue>
            </rData></Op>
            """;
        Assert.Throws<SoapParseException>(() => _parser.Parse(body));
    }

    [Fact]
    public void Country_code_not_two_chars_throws()
    {
        const string body = """
            <Op><rData>
              <accountNumber>AB</accountNumber>
              <destinationCountryCode>USA</destinationCountryCode>
              <weight>1</weight>
              <length>1</length><width>1</width><height>1</height>
              <packageValue>1</packageValue>
            </rData></Op>
            """;
        Assert.Throws<SoapParseException>(() => _parser.Parse(body));
    }

    [Fact]
    public void Invalid_uom_throws()
    {
        const string body = """
            <Op><rData>
              <accountNumber>AB</accountNumber>
              <destinationCountryCode>US</destinationCountryCode>
              <weight>1</weight>
              <weightUOM>Stones</weightUOM>
              <length>1</length><width>1</width><height>1</height>
              <packageValue>1</packageValue>
            </rData></Op>
            """;
        Assert.Throws<SoapParseException>(() => _parser.Parse(body));
    }
}
