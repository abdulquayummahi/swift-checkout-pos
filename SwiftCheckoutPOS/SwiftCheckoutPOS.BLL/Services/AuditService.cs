using System;
using System.Collections.Generic;
using System.Linq;
using SwiftCheckoutPOS.DAL.Repositories;

namespace SwiftCheckoutPOS.BLL.Services;

public sealed record AuditEventDTO(long Id, DateTime OccurredAt, string Actor, string Action, string EntityType, string EntityKey, string? Reason, string? Details);

/// <summary>BP-24/IMM-12: the UI records and reads audit events through the BLL, never through DAL entities.</summary>
public sealed class AuditService(AtomicUnit unit)
{
    public void Record(string actor, string action, string entityType, string entityKey, string? reason = null, string? details = null) =>
        unit.Run(() => unit.Audit(actor, action, entityType, entityKey, reason, details));

    public IReadOnlyList<AuditEventDTO> Recent(int take = 300, string? entityType = null) =>
        unit.RecentAudit(take, string.IsNullOrWhiteSpace(entityType) ? null : entityType.Trim())
            .Select(a => new AuditEventDTO(a.AuditEventId, a.OccurredAt, a.Actor, a.Action, a.EntityType, a.EntityKey, a.Reason, a.Details))
            .ToList();
}
