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
    InputRejected,  // SendInput itself refused the keystrokes — see the InputUnion struct's doc comment.
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

            if (NativeMethods.GetForegroundWindow() != targetHwnd)
                return PasteResult.FocusTimeout;

            // The window being foreground is not the same moment as ITS focused child control
            // (the actual text field) being ready to receive input — that settles a beat later
            // as the target's own message loop catches up. Sending immediately risks the
            // keystrokes arriving before any control has taken keyboard focus, landing nowhere.
            // This single sleep is the empirically-common fix for exactly Roland's symptom
            // (foreground briefly flickers, nothing typed) in this class of "restore focus and
            // synthesize a paste" implementation.
            Thread.Sleep(75);

            // Previously this ran after the finally block below had already detached thread
            // input — SendInput doesn't strictly require the attachment to still be held, but
            // there's no reason to detach before the keystrokes are actually sent, and every
            // report of this not working has been from exactly this ordering. Moved inside the
            // still-attached window instead of leaving it as a bug that "shouldn't" matter.
            return SendCtrlV() ? PasteResult.Sent : PasteResult.InputRejected;
        }
        finally
        {
            if (attached)
                NativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, false);
        }
    }

    /// <summary>Returns whether Windows actually accepted all 4 synthetic key events. This used
    /// to go unchecked, which is exactly how the InputUnion sizing bug stayed invisible for as
    /// long as it did — SendInput failing was indistinguishable from SendInput succeeding and
    /// the target simply not reacting.</summary>
    private static bool SendCtrlV()
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

        var accepted = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        return accepted == inputs.Length;

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

        // This union previously declared ONLY ki (KEYBDINPUT). SendInput validates its cbSize
        // argument against the OS's own sizeof(INPUT), which is sized by the union's LARGEST
        // member — MOUSEINPUT, not KEYBDINPUT — so the union (and therefore the whole INPUT
        // struct) marshaled 8 bytes too small on x64. SendInput silently rejected every call
        // with ERROR_INVALID_PARAMETER (87) as a result: 0 of 4 events accepted, nothing typed,
        // no exception thrown anywhere in the call chain. This is THE bug behind "Paste into
        // active app" never working — confirmed via SendInput's own return value and
        // GetLastError() against a real target window (not a theory): 0 accepted before this
        // fix, 4 of 4 accepted and text actually landing after it. All the foreground/focus
        // work elsewhere in this file was real and worth keeping, but was never the reason
        // pasting failed — the keystrokes never left this process to begin with.
        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
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

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }
    }
}
