using System.Diagnostics;
using System.Runtime.InteropServices;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;
using static ScreenRecorder.Infrastructure.Interop.NativeMethods;

namespace ScreenRecorder.Infrastructure.Capture;

/// <summary>
/// Enumerates displays and windows through Win32. Coordinates are physical pixels
/// because the application is Per-Monitor V2 DPI aware.
/// </summary>
public sealed class Win32CaptureSourceProvider : ICaptureSourceProvider
{
    public IReadOnlyList<DisplayInfo> GetDisplays()
    {
        var found = new List<(string Device, ScreenRegion Bounds, bool Primary)>();

        bool OnMonitor(nint monitor, nint hdc, ref Rect rect, nint data)
        {
            var info = new MonitorInfoEx { Size = Marshal.SizeOf<MonitorInfoEx>() };
            if (GetMonitorInfo(monitor, ref info))
            {
                var r = info.Monitor;
                found.Add((info.DeviceName, new ScreenRegion(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top),
                    (info.Flags & MonitorInfoFPrimary) != 0));
            }

            return true;
        }

        EnumDisplayMonitors(0, 0, OnMonitor, 0);

        return found
            .OrderByDescending(d => d.Primary)
            .ThenBy(d => d.Bounds.X)
            .ThenBy(d => d.Bounds.Y)
            .Select((d, i) => new DisplayInfo(d.Device, i + 1, d.Bounds, d.Primary))
            .ToList();
    }

    public IReadOnlyList<WindowInfo> GetWindows()
    {
        var ownProcessId = (uint)Environment.ProcessId;
        var windows = new List<WindowInfo>();

        bool OnWindow(nint hwnd, nint data)
        {
            if (IsRecordable(hwnd, ownProcessId, out var processId))
            {
                var title = GetTitle(hwnd);
                if (title.Length > 0)
                {
                    windows.Add(new WindowInfo(hwnd, title, GetProcessName(processId)));
                }
            }

            return true;
        }

        EnumWindows(OnWindow, 0);

        return windows
            .OrderBy(w => w.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(w => w.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static bool IsRecordable(nint hwnd, uint ownProcessId, out uint processId)
    {
        processId = 0;
        if (!IsWindowVisible(hwnd) || IsIconic(hwnd) || GetWindow(hwnd, GwOwner) != 0)
        {
            return false;
        }

        if ((GetWindowLongPtr(hwnd, GwlExStyle) & WsExToolWindow) != 0)
        {
            return false;
        }

        // Suspended UWP apps and windows on other virtual desktops are "cloaked": visible to Win32, not to users.
        if (DwmGetWindowAttribute(hwnd, DwmwaCloaked, out var cloaked, sizeof(int)) == 0 && cloaked != 0)
        {
            return false;
        }

        GetWindowThreadProcessId(hwnd, out processId);
        return processId != ownProcessId;
    }

    private static string GetTitle(nint hwnd)
    {
        var length = GetWindowTextLength(hwnd);
        if (length <= 0)
        {
            return string.Empty;
        }

        var buffer = new char[length + 1];
        var copied = GetWindowText(hwnd, buffer, buffer.Length);
        return new string(buffer, 0, copied).Trim();
    }

    private static string GetProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return string.Empty;
        }
    }
}
