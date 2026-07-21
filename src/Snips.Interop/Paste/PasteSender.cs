using System.Runtime.InteropServices;

namespace Snips.Interop.Paste;

public enum PasteResult
{
    Sent,
    TargetGone,
    AccessDenied, // UIPI: target runs elevated and we don't. See SPEC.md §6.2.
}

/// <summary>
/// Restores focus to a previously-foreground window and sends Ctrl+V, per SPEC.md §6.2.
/// This is inherently best-effort — see risk R4 in SPEC.md §12 — and cannot be verified from
/// an automated environment; it needs hands-on testing against the paste-target matrix (§13.2).
/// </summary>
public static class PasteSender
{
    public static PasteResult TrySendPaste(IntPtr targetHwnd, int delayMs)
    {
        if (targetHwnd == IntPtr.Zero || !NativeMethods.IsWindow(targetHwnd))
            return PasteResult.TargetGone;

        var targetThreadId = NativeMethods.GetWindowThreadProcessId(targetHwnd, out _);
        var currentThreadId = NativeMethods.GetCurrentThreadId();
        var attached = false;

        try
        {
            if (targetThreadId != 0 && targetThreadId != currentThreadId)
                attached = NativeMethods.AttachThreadInput(currentThreadId, targetThreadId, true);

            if (!NativeMethods.SetForegroundWindow(targetHwnd))
                return PasteResult.AccessDenied;

            NativeMethods.BringWindowToTop(targetHwnd);
        }
        finally
        {
            if (attached)
                NativeMethods.AttachThreadInput(currentThreadId, targetThreadId, false);
        }

        Thread.Sleep(delayMs);

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
