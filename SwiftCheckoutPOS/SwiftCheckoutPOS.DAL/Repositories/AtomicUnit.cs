using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.DAL.Context;
using SwiftCheckoutPOS.DAL.Entities;

namespace SwiftCheckoutPOS.DAL.Repositories;

/// <summary>
/// IMM-04/T06: runs one complete BLL use case as ONE transaction inside the EF execution strategy
/// (EnableRetryOnFailure stays on). Repositories sharing this scoped context may call SaveChanges inside;
/// nothing is committed until the whole operation succeeds. Each retry starts from a clean change tracker,
/// so the operation must (re)load what it changes.
/// </summary>
public sealed class AtomicUnit(ApplicationDbContext context)
{
    public T Run<T>(Func<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (context.Database.CurrentTransaction is not null) return operation(); // already inside a unit
        var strategy = context.Database.CreateExecutionStrategy();
        try
        {
            return strategy.Execute(() =>
            {
                context.ChangeTracker.Clear();
                using var transaction = context.Database.BeginTransaction();
                var result = operation();
                context.SaveChanges();
                transaction.Commit();
                return result;
            });
        }
        finally
        {
            context.ChangeTracker.Clear();
        }
    }

    public void Run(Action operation) => Run(() => { operation(); return true; });

    /// <summary>Stages an audit event in the current unit; it is saved with the action or not at all.</summary>
    public void Audit(string actor, string action, string entityType, string entityKey, string? reason = null, string? details = null) =>
        context.AuditEvents.Add(new AuditEvent
        {
            OccurredAt = DateTime.Now, Actor = Trim(actor, 100), Action = Trim(action, 50),
            EntityType = Trim(entityType, 50), EntityKey = Trim(entityKey, 100),
            Reason = reason is null ? null : Trim(reason, 200), Details = details is null ? null : Trim(details, 2000)
        });

    public IReadOnlyList<AuditEvent> RecentAudit(int take = 200, string? entityType = null) =>
        context.AuditEvents.AsNoTracking().Where(a => entityType == null || a.EntityType == entityType)
            .OrderByDescending(a => a.AuditEventId).Take(Math.Clamp(take, 1, 1000)).ToList();

    private static string Trim(string value, int max) => value.Length <= max ? value : value[..max];
}
