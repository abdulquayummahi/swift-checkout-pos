using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SwiftCheckoutPOS.Tests.Integration.Infrastructure;

/// <summary>Both requests finish reading/staging before either SaveChanges executes SQL. Retries never re-enter the barrier.</summary>
public sealed class DualSaveGate : SaveChangesInterceptor, IDisposable
{
    private readonly Barrier barrier = new(2);
    private int arrivals;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (Interlocked.Increment(ref arrivals) <= 2 && !barrier.SignalAndWait(TimeSpan.FromSeconds(15)))
            throw new InvalidOperationException("BP10 controlled race did not reach both save points within 15 seconds.");
        return result;
    }
    public void Dispose() => barrier.Dispose();
}
