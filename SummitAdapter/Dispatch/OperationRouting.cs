namespace SummitAdapter.Dispatch;

/// <summary>
/// What the adapter currently does with one SOAP operation. This is a per-operation switch so
/// operations can be ported off the legacy service <em>one at a time</em>: a not-yet-ported
/// operation is <see cref="PassThrough"/> (forwarded raw to the legacy service, Summit sees no
/// change); a ported operation is <see cref="Translate"/> (parsed and routed to GLP). The end goal
/// is every operation on <see cref="Translate"/>.
/// </summary>
public enum OperationRouting
{
    /// <summary>Forward the original SOAP request unchanged to the legacy service and relay its
    /// response verbatim. The adapter does not parse or translate.</summary>
    PassThrough,

    /// <summary>Parse the request and route it to GLP (this operation has been ported).</summary>
    Translate
}
