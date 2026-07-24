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

        parts.Add(KeyName(KeyInterop.KeyFromVirtualKey(virtualKey)));
        return string.Join("+", parts);
    }

    /// <summary>The raw Key enum name is what a user sees on the keycap, so it has to read like
    /// the physical key: "9", not "D9" (the enum name for the top-row 9); "Num 9", not "NumPad9";
    /// "," not "OemComma". Anything without a friendlier name (letters, F-keys, Space, …) already
    /// stringifies fine, so it falls through unchanged.</summary>
    private static string KeyName(Key key)
    {
        if (key is >= Key.D0 and <= Key.D9)
            return ((int)(key - Key.D0)).ToString();
        if (key is >= Key.NumPad0 and <= Key.NumPad9)
            return "Num " + (int)(key - Key.NumPad0);

        return key switch
        {
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.OemQuestion => "/",
            Key.OemSemicolon => ";",
            Key.OemQuotes => "'",
            Key.OemMinus => "-",
            Key.OemPlus => "=",
            Key.OemOpenBrackets => "[",
            Key.OemCloseBrackets => "]",
            Key.OemPipe => "\\",
            Key.OemTilde => "`",
            Key.Return => "Enter",
            Key.Next => "PageDown",
            Key.Prior => "PageUp",
            _ => key.ToString(),
        };
    }
}
