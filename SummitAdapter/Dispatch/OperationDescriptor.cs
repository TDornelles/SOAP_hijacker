namespace SummitAdapter.Dispatch;

/// <summary>
/// One registry entry: how a single SOAP operation is currently handled. Porting an operation off
/// the legacy service is a one-line change in <see cref="OperationRegistry"/> — flip its
/// <see cref="Routing"/> from <see cref="OperationRouting.PassThrough"/> to
/// <see cref="OperationRouting.Translate"/> and name its <see cref="GlpEndpoint"/>.
/// </summary>
/// <param name="Name">Operation name (the SOAPAction segment / body element local name).</param>
/// <param name="Routing">Pass-through to legacy, or translate to GLP.</param>
/// <param name="GlpEndpoint">Which GLP endpoint a translated operation forwards to;
///   <c>null</c> for pass-through operations (irrelevant until ported).</param>
/// <param name="WritesDb">True when the ported operation causes a write (Route/Ship); false for
///   read-only Rate. Meaningful only for <see cref="OperationRouting.Translate"/>.</param>
/// <param name="ResponseNamespace">XML namespace for the response operation/Result elements.</param>
public sealed record OperationDescriptor(
    string Name,
    OperationRouting Routing,
    GlpEndpoint? GlpEndpoint,
    bool WritesDb,
    string ResponseNamespace);
