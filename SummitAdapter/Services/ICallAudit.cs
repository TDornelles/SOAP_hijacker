namespace SummitAdapter.Services;

/// <summary>
/// Durable per-call audit sink. <see cref="Write"/> is called from the request path and must never
/// throw or block: a full queue or an I/O failure drops the line rather than affecting the caller.
/// </summary>
public interface ICallAudit
{
    void Write(CallAuditRecord record);
}
