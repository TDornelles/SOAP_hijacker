using SummitAdapter.Dispatch;
using SummitAdapter.Endpoints;
using SummitAdapter.Options;
using SummitAdapter.Services;
using SummitAdapter.Soap;

var builder = WebApplication.CreateBuilder(args);

// Configuration (section 7): the "Glp" and "Legacy" sections are environment-overridable; no
// secrets in source. "Legacy" points at the relocated origin for pass-through of un-ported ops.
builder.Services.Configure<GlpOptions>(builder.Configuration.GetSection(GlpOptions.SectionName));
builder.Services.Configure<LegacyOptions>(builder.Configuration.GetSection(LegacyOptions.SectionName));
builder.Services.Configure<AuditOptions>(builder.Configuration.GetSection(AuditOptions.SectionName));

// The GLP client stashes its request/response onto the in-flight audit record via HttpContext.
builder.Services.AddHttpContextAccessor();

// Durable per-call audit log — one JSON line per SOAP call to a daily-rolling file. Registered once
// and surfaced as both the sink (ICallAudit) and the background writer (IHostedService).
builder.Services.AddSingleton<CallAudit>();
builder.Services.AddSingleton<ICallAudit>(sp => sp.GetRequiredService<CallAudit>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<CallAudit>());

// Translator + router components — all stateless, registered as singletons.
builder.Services.AddSingleton<OperationRegistry>();
builder.Services.AddSingleton<SoapRequestParser>();
builder.Services.AddSingleton<SoapResponseBuilder>();
builder.Services.AddSingleton<SoapFaultBuilder>();
builder.Services.AddScoped<SoapHandler>();

// Typed GLP client; BaseAddress comes from config.
builder.Services.AddHttpClient<IGlpClient, GlpClient>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GlpOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});

// Typed legacy forwarder; BaseAddress is the relocated origin for pass-through operations.
builder.Services.AddHttpClient<ILegacyForwarder, LegacyForwarder>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<LegacyOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});

var app = builder.Build();

// The single endpoint for every operation (section 3). The operation is chosen by the SOAPAction
// header, not the path — so one route serves them all.
app.MapPost("/Routing/Service/Soap/V2.6/Routing.svc",
    (HttpContext context, SoapHandler handler) => handler.HandleAsync(context));

// Liveness probe for IIS warmup / load balancers / smoke tests — not part of the SOAP contract.
app.MapGet("/health", () => Results.Text("OK", "text/plain"));

app.Run();

// Exposed so the test project's WebApplicationFactory can boot an in-memory host.
public partial class Program { }
