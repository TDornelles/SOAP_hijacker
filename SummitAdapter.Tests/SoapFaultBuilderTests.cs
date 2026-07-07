using System.Xml.Linq;
using SummitAdapter.Soap;
using Xunit;

namespace SummitAdapter.Tests;

public class SoapFaultBuilderTests
{
    private readonly SoapFaultBuilder _builder = new();

    [Theory]
    [InlineData(SoapFaultCode.Client, "soap:Client")]
    [InlineData(SoapFaultCode.Server, "soap:Server")]
    public void Builds_valid_soap_11_fault(SoapFaultCode code, string expectedFaultCode)
    {
        var xml = _builder.Build(code, "something went wrong");

        var doc = XDocument.Parse(xml);
        XNamespace soap = SoapConstants.SoapEnvelopeNamespace;

        var fault = doc.Descendants(soap + "Fault").Single();
        Assert.Equal(expectedFaultCode, fault.Element("faultcode")!.Value);
        Assert.Equal("something went wrong", fault.Element("faultstring")!.Value);
    }

    [Fact]
    public void Fault_string_is_escaped()
    {
        var xml = _builder.Build(SoapFaultCode.Client, "bad <xml> & \"stuff\"");

        Assert.Contains("bad &lt;xml&gt; &amp;", xml);
        var doc = XDocument.Parse(xml);
        XNamespace soap = SoapConstants.SoapEnvelopeNamespace;
        Assert.Equal("bad <xml> & \"stuff\"", doc.Descendants(soap + "Fault").Single().Element("faultstring")!.Value);
    }
}
