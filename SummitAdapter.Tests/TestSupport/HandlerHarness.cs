using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SummitAdapter.Dispatch;
using SummitAdapter.Endpoints;
using SummitAdapter.Services;
using SummitAdapter.Soap;

namespace SummitAdapter.Tests.TestSupport;

/// <summary>Runs <see cref="SoapHandler"/> against a synthetic request without a full host.</summary>
public static class HandlerHarness
{
    public sealed record Result(int StatusCode, string? ContentType, string Body);

    public static SoapHandler Build(
        IGlpClient glp, ILegacyForwarder? legacy = null, OperationRegistry? registry = null) => new(
        registry ?? new OperationRegistry(),
        new SoapRequestParser(),
        new SoapResponseBuilder(),
        new SoapFaultBuilder(),
        glp,
        legacy ?? new FakeLegacyForwarder(),
        NullLogger<SoapHandler>.Instance);

    public static async Task<Result> InvokeAsync(
        IGlpClient glp, string body, string? soapAction,
        ILegacyForwarder? legacy = null, OperationRegistry? registry = null)
    {
        var handler = Build(glp, legacy, registry);

        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        if (soapAction is not null)
        {
            context.Request.Headers["SOAPAction"] = soapAction;
        }

        var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        await handler.HandleAsync(context);

        responseBody.Position = 0;
        var text = await new StreamReader(responseBody, Encoding.UTF8).ReadToEndAsync();
        return new Result(context.Response.StatusCode, context.Response.ContentType, text);
    }
}
