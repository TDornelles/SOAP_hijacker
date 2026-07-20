using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using SummitAdapter.Options;

namespace SummitAdapter.Services;

/// <summary>
/// Durable per-call audit log. Records are queued from the request path (bounded channel, never
/// blocking) and drained by a single background writer into a daily-rolling <c>rate-calls-*.jsonl</c>
/// file. Files older than <see cref="AuditOptions.RetentionDays"/> are pruned once a day. Every I/O
/// path is fail-safe — a logging failure is logged and swallowed, never surfaced to a live call.
/// </summary>
public sealed class CallAudit : BackgroundService, ICallAudit
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Local log file, not HTML — keep XML/JSON bodies readable rather than <-escaped.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly AuditOptions _options;
    private readonly ILogger<CallAudit> _logger;
    private readonly string _directory;

    // DropWrite: if the writer ever falls far behind, drop new lines rather than block a Summit call.
    private readonly Channel<CallAuditRecord> _channel =
        Channel.CreateBounded<CallAuditRecord>(new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true
        });

    private DateOnly _lastSweep = DateOnly.MinValue;

    public CallAudit(IOptions<AuditOptions> options, IHostEnvironment env, ILogger<CallAudit> logger)
    {
        _options = options.Value;
        _logger = logger;
        _directory = string.IsNullOrWhiteSpace(_options.Directory)
            ? Path.Combine(env.ContentRootPath, "call-logs")
            : _options.Directory!;
    }

    public void Write(CallAuditRecord record)
    {
        if (!_options.Enabled)
        {
            return;
        }

        // Never throws; returns false (line dropped) if the queue is full.
        _channel.Writer.TryWrite(record);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(_directory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Call audit disabled: cannot create log directory '{Dir}'.", _directory);
            return;
        }

        _logger.LogInformation("Call audit writing to '{Dir}' (retention {Days} days).",
            _directory, _options.RetentionDays);

        try
        {
            await foreach (var record in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                await WriteLineAsync(record, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    private async Task WriteLineAsync(CallAuditRecord record, CancellationToken cancellationToken)
    {
        try
        {
            var day = DateOnly.FromDateTime(record.Ts.UtcDateTime);
            var file = Path.Combine(_directory, $"rate-calls-{day:yyyyMMdd}.jsonl");
            var line = JsonSerializer.Serialize(record, Json) + Environment.NewLine;
            await File.AppendAllTextAsync(file, line, cancellationToken);
            SweepIfDue(day);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write call-audit line {Id}.", record.Id);
        }
    }

    /// <summary>Delete daily files past the retention window. Runs at most once per calendar day.</summary>
    private void SweepIfDue(DateOnly today)
    {
        if (today == _lastSweep || _options.RetentionDays <= 0)
        {
            return;
        }

        _lastSweep = today;
        var cutoff = DateTime.UtcNow.AddDays(-_options.RetentionDays);

        try
        {
            foreach (var file in Directory.EnumerateFiles(_directory, "rate-calls-*.jsonl"))
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    try { File.Delete(file); } catch { /* retried next sweep */ }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Call-audit retention sweep failed.");
        }
    }
}
