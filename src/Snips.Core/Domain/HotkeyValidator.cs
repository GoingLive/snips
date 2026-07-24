namespace Snips.Core.Domain;

/// <summary>
/// Validates a candidate global hotkey combo per SPEC.md §5.8, before ever attempting to
/// register it with the OS. Operates on raw Win32 MOD_*/VK_* values (conveniently identical
/// bit layout to WPF's ModifierKeys and KeyInterop.VirtualKeyFromKey), so it has no dependency
/// on WPF or Win32 interop and is fully unit-testable.
/// </summary>
public static class HotkeyValidator
{
    public const int ModAlt = 0x1;
    public const int ModControl = 0x2;
    public const int ModShift = 0x4;
    public const int ModWin = 0x8;

    private const int VkDelete = 0x2E;
    private const int VkTab = 0x09;
    private const int VkEscape = 0x1B;
    private const int VkL = 0x4C;
    private const int VkD = 0x44;
    private const int VkG = 0x47;
    private const int VkF4 = 0x73;
    private const int VkSnapshot = 0x2C; // PrtScn
    private const int VkF1 = 0x70;
    private const int VkF24 = 0x87;

    /// <summary>Combinations Windows intercepts before any application sees them, or that
    /// would be too disruptive to reassign. Listed explicitly in SPEC.md §5.8.</summary>
    private static readonly HashSet<(int Modifiers, int VirtualKey)> ReservedCombos =
    [
        (ModControl | ModAlt, VkDelete), // Ctrl+Alt+Del
        (ModWin, VkL),                   // Win+L
        (ModWin, VkD),                   // Win+D
        (ModWin, VkTab),                 // Win+Tab
        (ModAlt, VkTab),                 // Alt+Tab
        (ModControl | ModShift, VkEscape), // Ctrl+Shift+Esc
        (ModWin, VkG),                   // Win+G
        (ModAlt, VkF4),                  // Alt+F4
        (0, VkSnapshot),                 // PrtScn
    ];

    public static bool IsReserved(int modifiers, int virtualKey) =>
        ReservedCombos.Contains((modifiers, virtualKey));

    public static bool IsFunctionKey(int virtualKey) => virtualKey is >= VkF1 and <= VkF24;

    /// <summary>SPEC.md §5.8: "Requires at least one modifier, or a bare F1–F24."</summary>
    public static bool HasRequiredModifierOrIsFunctionKey(int modifiers, int virtualKey) =>
        modifiers != 0 || IsFunctionKey(virtualKey);

    public static bool IsValid(int modifiers, int virtualKey) =>
        HasRequiredModifierOrIsFunctionKey(modifiers, virtualKey) && !IsReserved(modifiers, virtualKey);

    /// <summary>Ctrl+Alt (with no Shift) is what a physical AltGr key actually sends on most
    /// non-US keyboard layouts — Windows cannot distinguish "the user pressed Ctrl and Alt
    /// separately" from "the user pressed AltGr." On many of those layouts (German, French,
    /// Italian, Spanish among them) the same combination is already bound to typing a character
    /// (e.g. Swiss German AltGr+2 types '@'), which can race against or defeat a global hotkey
    /// registered on exactly that combo. This can only ever be advisory, never a hard block:
    /// whether it's actually a problem depends on the user's own physical keyboard layout, which
    /// isn't something Snips can know from the virtual key alone. An F-key is exempt — AltGr
    /// compositions are for printable characters, never function keys.</summary>
    public static bool IsLikelyAltGrCollision(int modifiers, int virtualKey) =>
        modifiers == (ModControl | ModAlt) && !IsFunctionKey(virtualKey);
}
