namespace SummitAdapter.Dispatch;

/// <summary>Which GLP endpoint a <em>translated</em> operation forwards to.</summary>
public enum GlpEndpoint
{
    /// <summary>Read-only price check — GLP's rate path, no DB write.</summary>
    Rate,

    /// <summary>Creates the delivery — GLP's ship path; GLP writes tbl_consignee.</summary>
    Ship
}
