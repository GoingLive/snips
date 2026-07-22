using System.Windows.Input;
using Snips.Core.Domain;

namespace Snips.App;

internal static class HotkeyFormatting
{
    public static string Format(int modifiers, int virtualKey)
    {
        var parts = new List<string>();
        if ((modifiers & HotkeyValidator.ModControl) != 0) parts.Add("Ctrl");
        if ((modifiers & HotkeyValidator.ModAlt) != 0) parts.Add("Alt");
        if ((modifiers & HotkeyValidator.ModShift) != 0) parts.Add("Shift");
        if ((modifiers & HotkeyValidator.ModWin) != 0) parts.Add("Win");

        parts.Add(KeyInterop.KeyFromVirtualKey(virtualKey).ToString());
        return string.Join("+", parts);
    }
}
