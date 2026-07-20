namespace SummitAdapter.Options;

/// <summary>
/// Configuration for the durable per-call audit log (section "Audit"). One JSON line per SOAP call
/// is written to a daily-rolling file; older files are pruned after <see cref="RetentionDays"/>.
/// </summary>
public sealed class AuditOptions
{
    public const string SectionName = "Audit";

    /// <summary>Master switch. When false, nothing is captured or written (default true).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Directory for the <c>rate-calls-yyyyMMdd.jsonl</c> files. Use an ABSOLUTE path OUTSIDE the
    /// deployed app folder in production so a redeploy (which overwrites <c>.\app</c>) never wipes
    /// history. If null/empty, a <c>call-logs</c> folder under the content root is used.
    /// </summary>
    public string? Directory { get; set; }

    /// <summary>Daily files older than this are auto-deleted. Default 180 (~6 months). 0 = keep all.</summary>
    public int RetentionDays { get; set; } = 180;
}
