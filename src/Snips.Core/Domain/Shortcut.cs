namespace Snips.Core.Domain;

/// <summary>Mirrors the Shortcut table in SPEC.md §4.3. Modifiers/VirtualKey use raw Win32
/// MOD_* / VK_* values so this stays free of any WPF or Win32 interop dependency.</summary>
public sealed class Shortcut
{
    public required string Id { get; set; }
    public required string SnippetId { get; set; }
    public required int Modifiers { get; set; }
    public required int VirtualKey { get; set; }
    public bool IsEnabled { get; set; } = true;
}
