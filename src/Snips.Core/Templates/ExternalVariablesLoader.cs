using System.Text.Json;

namespace Snips.Core.Templates;

/// <summary>
/// Reads a flat JSON name-&gt;value map that another process (or the user by hand) can place
/// next to snips.db, for values Snips has no other way to know — e.g. a work email pulled
/// from a company directory that isn't stored anywhere on the machine itself. Re-read fresh on
/// every render: the whole point is that another app can update the file and have Snips notice
/// on the very next paste, and the file is expected to be tiny, so re-parsing it every time
/// costs nothing measurable. A missing or malformed file degrades to "no external variables"
/// rather than failing the paste — SPEC.md §1.1.
/// </summary>
public static class ExternalVariablesLoader
{
    public static IReadOnlyDictionary<string, string>? TryLoad(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            // Explicit, rather than relying on JsonSerializerOptions.PropertyNameCaseInsensitive
            // (whose effect on dictionary-key comparison specifically isn't reliably documented) —
            // every other variable-name lookup in the engine is case-insensitive, so this must be too.
            return raw is null ? null : new Dictionary<string, string>(raw, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
