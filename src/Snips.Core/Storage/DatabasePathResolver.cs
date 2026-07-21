namespace Snips.Core.Storage;

/// <summary>Chooses the snips.db location per SPEC.md §4.4. Pure function — no environment access — so it's testable.</summary>
public static class DatabasePathResolver
{
    public const string PortableMarkerFileName = "portable.txt";
    public const string DatabaseFileName = "snips.db";

    /// <param name="exeDirectory">Directory containing Snips.exe.</param>
    /// <param name="localAppDataDirectory">Environment.GetFolderPath(SpecialFolder.LocalApplicationData).</param>
    /// <param name="portableMarkerExists">Whether <see cref="PortableMarkerFileName"/> exists next to the exe.</param>
    public static string Resolve(string exeDirectory, string localAppDataDirectory, bool portableMarkerExists) =>
        portableMarkerExists
            ? Path.Combine(exeDirectory, DatabaseFileName)
            : Path.Combine(localAppDataDirectory, "Snips", DatabaseFileName);
}
