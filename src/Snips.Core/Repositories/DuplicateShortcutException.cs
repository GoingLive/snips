namespace Snips.Core.Repositories;

/// <summary>Thrown when a combo (Modifiers, VirtualKey) is already assigned to a different snippet (IX_Shortcut_Combo).</summary>
public sealed class DuplicateShortcutException(string combo)
    : Exception($"The combination {combo} is already assigned to another snippet.")
{
    public string Combo { get; } = combo;
}
