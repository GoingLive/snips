namespace Snips.Core.Repositories;

/// <summary>Thrown when a snippet name collides case-insensitively with an existing one (IX_Snippet_Name).</summary>
public sealed class DuplicateSnippetNameException(string name)
    : Exception($"A snippet named '{name}' already exists.")
{
    public string Name { get; } = name;
}
