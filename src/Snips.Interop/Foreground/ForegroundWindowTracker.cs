using System.Runtime.InteropServices;

namespace Snips.Interop.Foreground;

/// <summary>
/// Tracks the last foreground window that does not belong to this process, per SPEC.md §6.1.
/// More reliable than calling GetForegroundWindow() at paste time, because by then Snips'
/// own picker window may already have taken focus.
/// </summary>
public sealed class ForegroundWindowTracker : IDisposable
{
    private const uint EVENT_SYSTEM_FOREGROUND = 3;
    private const uint WINEVENT_OUTOFCONTEXT = 0;

    private readonly int _ownProcessId = Environment.ProcessId;
    private readonly NativeMethods.WinEventDelegate _callback;
    private readonly IntPtr _hookHandle;

    public IntPtr? LastExternalForegroundWindow { get; private set; }

    public ForegroundWindowTracker()
    {
        _callback = OnForegroundChanged;
        _hookHandle = NativeMethods.SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero,
            _callback, 0, 0, WINEVENT_OUTOFCONTEXT);

        // The hook only fires on subsequent CHANGES. Without this, whatever app was already
        // focused when Snips started (the common case — nobody switches windows just to launch
        // a tray app) would never be captured, and the very first paste attempt would have no
        // target at all.
        var current = NativeMethods.GetForegroundWindow();
        if (current != IntPtr.Zero)
        {
            NativeMethods.GetWindowThreadProcessId(current, out var processId);
            if (processId != _ownProcessId)
                LastExternalForegroundWindow = current;
        }
    }

    private void OnForegroundChanged(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (hwnd == IntPtr.Zero)
            return;

        NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
        if (processId != _ownProcessId)
            LastExternalForegroundWindow = hwnd;
    }

    public void Dispose()
    {
        if (_hookHandle != IntPtr.Zero)
            NativeMethods.UnhookWinEvent(_hookHandle);
    }

    private static class NativeMethods
    {
        public delegate void WinEventDelegate(
            IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWinEventHook(
            uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
    }
}
