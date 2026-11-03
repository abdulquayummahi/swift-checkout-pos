using System.Collections.Concurrent;

namespace SwiftCheckoutPOS.BLL.Security;

/// <summary>
/// BP-22 (PR-Q78/CQ-09): per username. After the 5th failure wait 30 s; each further failure doubles the wait
/// (60, 120, 240 s) up to 5 minutes. No permanent lockout. Resets on success or after 30 minutes without a failure.
/// One shop PC: kept in memory (a restart clears it).
/// </summary>
public sealed class LoginThrottle
{
    private sealed record State(int Failures, DateTime LastFailure, DateTime BlockedUntil);
    private readonly ConcurrentDictionary<string, State> states = new(StringComparer.OrdinalIgnoreCase);

    public TimeSpan WaitRemaining(string username, DateTime now)
    {
        if (!states.TryGetValue(Key(username), out var s)) return TimeSpan.Zero;
        if (now - s.LastFailure > TimeSpan.FromMinutes(30)) { states.TryRemove(Key(username), out _); return TimeSpan.Zero; }
        return s.BlockedUntil > now ? s.BlockedUntil - now : TimeSpan.Zero;
    }

    public TimeSpan RegisterFailure(string username, DateTime now)
    {
        var s = states.AddOrUpdate(Key(username),
            _ => new State(1, now, now),
            (_, old) =>
            {
                var failures = now - old.LastFailure > TimeSpan.FromMinutes(30) ? 1 : old.Failures + 1;
                return new State(failures, now, now + Delay(failures));
            });
        return s.BlockedUntil > now ? s.BlockedUntil - now : TimeSpan.Zero;
    }

    public void Reset(string username) => states.TryRemove(Key(username), out _);

    public static TimeSpan Delay(int failures) => failures < 5 ? TimeSpan.Zero
        : TimeSpan.FromSeconds(Math.Min(300, 30 * Math.Pow(2, failures - 5)));

    private static string Key(string username) => (username ?? "").Trim();
}
