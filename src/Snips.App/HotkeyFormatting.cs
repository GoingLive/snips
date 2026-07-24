using System.Windows.Input;
using Snips.Core.Domain;

namespace Snips.App;

internal static class HotkeyFormatting
{
    public static string Format(int modifiers, int virtualKey)
    {
        var parts = new List<string>();
        if ((modifiers & HotkeyValidator.ModControl) != 0) parts.Add(UiStrings.Get("Str_HotkeyCtrl"));
        if ((modifiers & HotkeyValidator.ModAlt) != 0) parts.Add(UiStrings.Get("Str_HotkeyAlt"));
        if ((modifiers & HotkeyValidator.ModShift) != 0) parts.Add(UiStrings.Get("Str_HotkeyShift"));
        if ((modifiers & HotkeyValidator.ModWin) != 0) parts.Add(UiStrings.Get("Str_HotkeyWin"));

        parts.Add(KeyName(KeyInterop.KeyFromVirtualKey(virtualKey)));
        return string.Join("+", parts);
    }

    /// <summary>The raw Key enum name is what a user sees on the keycap, so it has to read like
    /// the physical key: "9", not "D9" (the enum name for the top-row 9); "Num 9", not "NumPad9";
    /// "," not "OemComma". Anything without a friendlier name (letters, F-keys, Space, …) falls
    /// through unchanged — the .NET Key enum's own names for those aren't localized here (a
    /// known, accepted gap; see the localization audit).</summary>
    private static string KeyName(Key key)
    {
        if (key is >= Key.D0 and <= Key.D9)
            return ((int)(key - Key.D0)).ToString();
        if (key is >= Key.NumPad0 and <= Key.NumPad9)
            return UiStrings.Get("Str_HotkeyNumPrefix") + (int)(key - Key.NumPad0);

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
            Key.Return => UiStrings.Get("Str_HotkeyEnter"),
            Key.Next => UiStrings.Get("Str_HotkeyPageDown"),
            Key.Prior => UiStrings.Get("Str_HotkeyPageUp"),
            _ => key.ToString(),
        };
    }
}
