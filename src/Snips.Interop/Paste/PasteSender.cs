using System.Runtime.InteropServices;

namespace Snips.Interop.Paste;

public enum PasteResult
{
    Sent,
    TargetGone,
    // Windows refused to give the target foreground. This used to be labelled purely as UIPI
    // elevation mismatch, but that's only one of several causes — Windows' general
    // foreground-lock heuristic can reject the request even between two completely ordinary,
    // unelevated windows. Renamed from AccessDenied so the caller doesn't have to keep
    // asserting a specific cause the code can't actually distinguish.
    ForegroundDenied,
    FocusTimeout,  // SetForegroundWindow "succeeded" but the target never actually became foreground in time.
}

/// <summary>
/// Restores focus to a previously-foreground window and sends Ctrl+V, per SPEC.md §6.2.
/// This is inherently best-effort — see risk R4 in SPEC.md §12 — and cannot be verified from
/// an automated environment; it needs hands-on testing against the paste-target matrix (§13.2).
/// </summary>
public static class PasteSender
{
    public static PasteResult TrySendPaste(IntPtr targetHwnd, int timeoutMs)
    {
        if (targetHwnd == IntPtr.Zero || !NativeMethods.IsWindow(targetHwnd))
            return PasteResult.TargetGone;

        // Windows only allows SetForegroundWindow to succeed for a thread that currently has
        // (or recently had) input focus standing — the calling thread here (Snips, mid paste)
        // usually does, since the user just clicked/pressed Enter in its window, but that
        // standing belongs to whichever thread currently owns the foreground window, not to the
        // target we're trying to switch to. AttachThreadInput needs to borrow standing FROM the
        // current foreground thread, not synchronize with the target's thread — attaching to the
        // target does nothing for this specific permission check.
        var foregroundThreadId = NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out _);
        var currentThreadId = NativeMethods.GetCurrentThreadId();
        var attached = false;

        try
        {
            if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId)
                attached = NativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, true);

            if (!NativeMethods.SetForegroundWindow(targetHwnd))
                return PasteResult.ForegroundDenied;

            NativeMethods.BringWindowToTop(targetHwnd);

            // SetForegroundWindow returning true only means Windows accepted the request, not
            // that the target has actually finished becoming foreground yet — sending input
            // before that settles is a real race. It either silently drops the synthetic Ctrl+V,
            // or lets a stray keystroke the user is still physically releasing (e.g. the Enter
            // that triggered this apply) land in the target instead of us. Poll for confirmation
            // instead of guessing a fixed delay.
            var deadline = Environment.TickCount64 + timeoutMs;
            while (NativeMethods.GetForegroundWindow() != targetHwnd && Environment.TickCount64 < deadline)
                Thread.Sleep(10);
        }
        finally
        {
            if (attached)
                NativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, false);
        }

        if (NativeMethods.GetForegroundWindow() != targetHwnd)
            return PasteResult.FocusTimeout;

        SendCtrlV();
        return PasteResult.Sent;
    }

    private static void SendCtrlV()
    {
        const ushort VK_CONTROL = 0x11;
        const ushort VK_V = 0x56;
        const uint KEYEVENTF_KEYUP = 0x0002;

        var inputs = new[]
        {
            KeyInput(VK_CONTROL, 0),
            KeyInput(VK_V, 0),
            KeyInput(VK_V, KEYEVENTF_KEYUP),
            KeyInput(VK_CONTROL, KEYEVENTF_KEYUP),
        };

        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());

        static NativeMethods.INPUT KeyInput(ushort vk, uint flags) => new()
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT { wVk = vk, dwFlags = flags },
            },
        };
    }

    private static class NativeMethods
    {
        public const uint INPUT_KEYBOARD = 1;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        [DllImport("kernel32.dll")]
        public static extern int GetCurrentThreadId();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AttachThreadInput(int idAttach, int idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
    }
}
