using System.Runtime.InteropServices;

namespace Snips.Interop.Clipboard;

/// <summary>
/// Plain-text-only clipboard backup/restore for the auto-paste path (SPEC.md §6.4). Rich
/// formats (RTF/HTML/DIB/file-drop) arrive with the rich-text pipeline in Phase 3 — restoring
/// only plain text for now is a deliberate, documented scope reduction, not an oversight.
/// Best-effort by design: a busy clipboard (another app mid-write) must never block a paste.
/// </summary>
public static class ClipboardTextGuard
{
    public static string? TryGetCurrentText()
    {
        try
        {
            return System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText() : null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    public static void SetText(string text)
    {
        try
        {
            System.Windows.Clipboard.SetText(text);
        }
        catch (COMException)
        {
            // Another process is holding the clipboard open; nothing we can do about that here.
        }
    }

    /// <summary>Waits delayMs, then restores previousText (or clears the clipboard if there was none).</summary>
    public static async Task RestoreAfterAsync(string? previousText, int delayMs, CancellationToken ct = default)
    {
        await Task.Delay(delayMs, ct);

        try
        {
            if (previousText is not null)
                System.Windows.Clipboard.SetText(previousText);
            else
                System.Windows.Clipboard.Clear();
        }
        catch (COMException)
        {
        }
    }
}
