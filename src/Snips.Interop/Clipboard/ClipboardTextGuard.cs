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

    /// <summary>
    /// Returns false if every attempt failed. The Windows clipboard is a single shared
    /// resource — OpenClipboard commonly fails transiently for a few milliseconds while another
    /// process (clipboard history, a clipboard manager, anti-virus scanning a copy) is holding
    /// it, not just when something is broken. A single try-once call was silently losing writes
    /// under exactly this kind of contention, so this retries briefly before giving up for real.
    /// </summary>
    public static bool SetText(string text)
    {
        const int maxAttempts = 10;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                System.Windows.Clipboard.SetText(text);
                return true;
            }
            catch (COMException) when (attempt < maxAttempts)
            {
                Thread.Sleep(15);
            }
            catch (COMException)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>Waits delayMs, then restores previousText (or clears the clipboard if there was none).</summary>
    public static async Task RestoreAfterAsync(string? previousText, int delayMs, CancellationToken ct = default)
    {
        await Task.Delay(delayMs, ct);

        if (previousText is not null)
            SetText(previousText);
        else
            TryClear();
    }

    private static void TryClear()
    {
        try
        {
            System.Windows.Clipboard.Clear();
        }
        catch (COMException)
        {
        }
    }
}
