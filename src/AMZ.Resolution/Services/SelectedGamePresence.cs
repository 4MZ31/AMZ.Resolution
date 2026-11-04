using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using AMZ.Resolution.Core;

namespace AMZ.Resolution.Services;

internal sealed class SelectedGamePresence : IGamePresence
{
    private volatile GameProcessMatcher matcher = new([]);
    private readonly int sessionId = GetCurrentSessionId();
    public bool HasSelection => matcher.HasSelection;
    public void Update(IEnumerable<GameEntry> games) => matcher = new(games);
    private static int GetCurrentSessionId()
    {
        using var current = Process.GetCurrentProcess();
        return current.SessionId;
    }

    public bool IsRunning()
    {
        var current = matcher;
        foreach (var name in current.ProcessNames)
        {
            var processes = Process.GetProcessesByName(name);
            try
            {
                foreach (var process in processes)
                {
                    try
                    {
                        if (process.SessionId != sessionId) continue;
                        if (current.Matches(name, null) || current.Matches(name, ImagePath(process.Id))) return true;
                    }
                    catch (InvalidOperationException) { }
                    catch (Win32Exception) { }
                }
            }
            finally { foreach (var process in processes) process.Dispose(); }
        }
        return false;
    }

    private static string? ImagePath(int processId)
    {
        using var handle = OpenProcess(0x1000, false, processId); // QUERY_LIMITED_INFORMATION
        if (handle.IsInvalid) return null;
        var length = 32768;
        var buffer = new StringBuilder(length);
        return QueryFullProcessImageName(handle, 0, buffer, ref length) ? buffer.ToString() : null;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int id);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process, int flags, StringBuilder name, ref int size);
}
