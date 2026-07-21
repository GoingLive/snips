using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Snips.Interop.Hotkeys;

/// <summary>
/// Registers system-wide hotkeys against a window's message loop (WM_HOTKEY) per SPEC.md §5.8.
/// One instance per window; each Register call can fail independently if the combination is
/// already claimed by another application — the caller is expected to surface that per-shortcut
/// (§5.8: "saved but marked ⚠ inactive"), not treat it as fatal to the whole app.
/// </summary>
public sealed partial class HotKeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_NOREPEAT = 0x4000;

    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _callbacks = [];
    private int _nextId = 0xA000; // RegisterHotKey's valid app-defined id range is 0x0000-0xBFFF
    private bool _disposed;

    public HotKeyManager(Window window)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(handle) ?? throw new InvalidOperationException("Window has no HWND yet.");
        _source.AddHook(WndProc);
    }

    /// <summary>Returns the registration id on success, or null if the combination could not be claimed.</summary>
    public int? Register(HotKeyModifiers modifiers, uint virtualKey, Action onPressed)
    {
        var id = _nextId++;
        var fsModifiers = (uint)modifiers | MOD_NOREPEAT;

        if (!NativeMethods.RegisterHotKey(_source.Handle, id, fsModifiers, virtualKey))
            return null;

        _callbacks[id] = onPressed;
        return id;
    }

    public void Unregister(int id)
    {
        if (_callbacks.Remove(id))
            NativeMethods.UnregisterHotKey(_source.Handle, id);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _callbacks.TryGetValue((int)wParam, out var callback))
        {
            callback();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        foreach (var id in _callbacks.Keys.ToList())
            NativeMethods.UnregisterHotKey(_source.Handle, id);
        _callbacks.Clear();

        _source.RemoveHook(WndProc);
    }

    private static partial class NativeMethods
    {
        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
