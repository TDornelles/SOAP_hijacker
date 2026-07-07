using System.Text;
using SummitAdapter.Dispatch;
using SummitAdapter.Models;
using SummitAdapter.Services;
using SummitAdapter.Soap;

namespace SummitAdapter.Endpoints;

/// <summary>
/// The single <c>.svc</c> request handler — the request lifecycle from section 5. It is a selective
/// hijacker: for operations that have been ported it translates (read raw XML, parse rData, forward
/// to GLP, build the SOAP response); for every other operation it forwards the request unchanged to
/// the legacy service and relays the response verbatim. Every error path returns a SOAP 1.1 Fault
/// with text/xml, never a bare HTTP page.
/// </summary>
public sealed class SoapHandler
{
    private readonly OperationRegistry _registry;
    private readonly SoapRequestParser _parser;
    private readonly SoapResponseBuilder _responseBuilder;
    private readonly SoapFaultBuilder _faultBuilder;
    private readonly IGlpClient _glp;
    private readonly ILegacyForwarder _legacy;
    private readonly ILogger<SoapHandler> _logger;

    public SoapHandler(
        OperationRegistry registry,
        SoapRequestParser parser,
        SoapResponseBuilder responseBuilder,
        SoapFaultBuilder faultBuilder,
        IGlpClient glp,
        ILegacyForwarder legacy,
        ILogger<SoapHandler> logger)
    {
        _registry = registry;
        _parser = parser;
        _responseBuilder = responseBuilder;
        _faultBuilder = faultBuilder;
        _glp = glp;
        _legacy = legacy;
        _logger = logger;
    }

    public async Task HandleAsync(HttpContext context)
    {
        var cancellationToken = context.RequestAborted;

        // 1. Read the raw request body as UTF-8 text.
        string body;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
        {
            body = await reader.ReadToEndAsync(cancellationToken);
        }

        // 2. Determine the operation name (SOAPAction, then body fallback).
        string operationName;
        try
        {
            var soapAction = context.Request.Headers["SOAPAction"].ToString();
            operationName = OperationResolver.Resolve(soapAction, body);
        }
        catch (SoapParseException ex)
        {
            await WriteFaultAsync(context, SoapFaultCode.Client, ex.Message);
            return;
        }

        // 3. Decide how this operation is handled. The adapter only hijacks operations that have
        //    been ported to GLP; anything else — including an operation not listed in the registry —
        //    is forwarded to the legacy service untouched so Summit sees no change.
        if (!_registry.TryGet(operationName, out var descriptor)
            || descriptor.Routing == OperationRouting.PassThrough)
        {
            await ForwardToLegacyAsync(context, operationName, body);
            return;
        }

        // 4. Ported operation → parse <rData> into the package DTO.
        PackageRequest request;
        try
        {
            request = _parser.Parse(body);
        }
        catch (SoapParseException ex)
        {
            await WriteFaultAsync(context, SoapFaultCode.Client, ex.Message);
            return;
        }

        // 5/6. Forward to GLP (rate vs ship per the registry); non-success → Server Fault.
        LandedCostResult result;
        try
        {
            result = await _glp.SendAsync(descriptor.GlpEndpoint!.Value, request, cancellationToken);
        }
        catch (GlpException ex)
        {
            _logger.LogError(ex,
                "GLP call for {Operation} failed. Status={Status} Body={Body}",
                operationName, ex.StatusCode, ex.ResponseBody);
            await WriteFaultAsync(context, SoapFaultCode.Server, "Downstream rating service error.");
            return;
        }

        // 7. Build the SOAP success envelope and return it.
        var xml = _responseBuilder.Build(descriptor, result);
        await WriteSoapAsync(context, StatusCodes.Status200OK, xml);
    }

    /// <summary>
    /// Relay an un-ported operation to the legacy service and return its response verbatim. A
    /// forwarding failure becomes a SOAP 1.1 Server Fault so Summit still gets an XML envelope.
    /// </summary>
    private async Task ForwardToLegacyAsync(HttpContext context, string operationName, string body)
    {
        var soapAction = context.Request.Headers["SOAPAction"].ToString();
        var contentType = context.Request.ContentType ?? SoapConstants.XmlContentType;

        try
        {
            var relayed = await _legacy.ForwardAsync(
                context.Request.Path.ToString(), soapAction, contentType, body, context.RequestAborted);

            context.Response.StatusCode = relayed.StatusCode;
            context.Response.ContentType = relayed.ContentType;
            await context.Response.WriteAsync(relayed.Body, Encoding.UTF8, context.RequestAborted);
        }
        catch (LegacyForwardException ex)
        {
            _logger.LogError(ex, "Pass-through of {Operation} to the legacy service failed.", operationName);
            await WriteFaultAsync(context, SoapFaultCode.Server, "Downstream service unavailable.");
        }
    }

    private Task WriteFaultAsync(HttpContext context, SoapFaultCode code, string message)
    {
        var xml = _faultBuilder.Build(code, message);
        // SOAP 1.1 faults are conventionally returned with HTTP 500.
        return WriteSoapAsync(context, StatusCodes.Status500InternalServerError, xml);
    }

    private static async Task WriteSoapAsync(HttpContext context, int statusCode, string xml)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = SoapConstants.XmlContentType;
        await context.Response.WriteAsync(xml, Encoding.UTF8, context.RequestAborted);
    }
}
