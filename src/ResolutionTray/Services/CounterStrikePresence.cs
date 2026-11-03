using System.ComponentModel;
using System.Diagnostics;
using ResolutionTray.Core;

namespace ResolutionTray.Services;

internal sealed class CounterStrikePresence : IGamePresence
{
    private readonly int sessionId = GetCurrentSessionId();
    private static int GetCurrentSessionId()
    {
        using var current = Process.GetCurrentProcess();
        return current.SessionId;
    }

    public bool IsRunning()
    {
        foreach (var name in new[] { "cs2", "csgo" })
        {
            var processes = Process.GetProcessesByName(name);
            try
            {
                foreach (var process in processes)
                {
                    try { if (process.SessionId == sessionId) return true; }
                    catch (InvalidOperationException) { } // Process exited during enumeration.
                    catch (Win32Exception) { } // A process belonging to another user may be inaccessible.
                }
            }
            finally { foreach (var process in processes) process.Dispose(); }
        }
        return false;
    }
}
