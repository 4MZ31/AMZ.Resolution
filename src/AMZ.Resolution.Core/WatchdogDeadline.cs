namespace AMZ.Resolution.Core;

/// <summary>Measured by the watchdog, independently of the UI and the system clock.</summary>
public sealed class WatchdogDeadline
{
    private TimeSpan lastHeartbeat;
    public bool ShouldRecover(TimeSpan elapsed, bool heartbeat, bool confirmed)
    {
        if (heartbeat) lastHeartbeat = elapsed;
        return elapsed - lastHeartbeat >= TimeSpan.FromSeconds(30)
            || !confirmed && elapsed >= TimeSpan.FromSeconds(60);
    }
}
