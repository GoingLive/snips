namespace Snips.Core.Storage;

/// <summary>Chooses the snips.db location per SPEC.md §4.4. Pure function — no environment access — so it's testable.</summary>
public static class DatabasePathResolver
{
    public const string PortableMarkerFileName = "portable.txt";
    public const string DatabaseFileName = "snips.db";
    public const string ExternalVariablesFileName = "external-variables.json";

    /// <param name="exeDirectory">Directory containing Snips.exe.</param>
    /// <param name="localAppDataDirectory">Environment.GetFolderPath(SpecialFolder.LocalApplicationData).</param>
    /// <param name="portableMarkerExists">Whether <see cref="PortableMarkerFileName"/> exists next to the exe.</param>
    public static string Resolve(string exeDirectory, string localAppDataDirectory, bool portableMarkerExists) =>
        portableMarkerExists
            ? Path.Combine(exeDirectory, DatabaseFileName)
            : Path.Combine(localAppDataDirectory, "Snips", DatabaseFileName);

    /// <summary>Sits next to snips.db (whichever mode chose it), so another process — or the
    /// user by hand — has one obvious, mode-agnostic place to drop externally-supplied
    /// variable values. See docs/variables.yaml "not_yet_implemented" and the 2026-07-22
    /// proposal for the intended use.</summary>
    public static string ResolveExternalVariablesPath(string databasePath) =>
        Path.Combine(Path.GetDirectoryName(databasePath) ?? ".", ExternalVariablesFileName);
}
