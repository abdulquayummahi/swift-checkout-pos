using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SwiftCheckoutPOS.DAL.Entities;

namespace SwiftCheckoutPOS.Tests.Integration.Infrastructure;

public sealed class InjectedTestFailure : Exception
{
    public InjectedTestFailure() : base("BP10 injected failure; no sensitive diagnostics.") { }
}

/// <summary>Fail after real SQL SaveChanges, while the repository's outer transaction remains uncommitted.</summary>
public sealed class FailAfterSave : SaveChangesInterceptor
{
    public int Calls { get; private set; }
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Calls++;
        throw new InjectedTestFailure();
    }
}

/// <summary>Only the ledger stage fails, exposing the existing independent product commit.</summary>
public sealed class FailLedgerSave : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context!.ChangeTracker.Entries<LedgerEntry>().Any(e => e.State == EntityState.Added))
            throw new InjectedTestFailure();
        return result;
    }
}
