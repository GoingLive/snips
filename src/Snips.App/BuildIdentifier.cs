using System.IO;
using System.Reflection;

namespace Snips.App;

/// <summary>
/// A human-readable stamp of exactly which build is running, so two people looking at Snips
/// can confirm they're talking about the same thing (Roland's request, 2026-07-22). There's no
/// formal release versioning yet (Phase 6), so this uses the exe's own file timestamp — always
/// accurate the moment `dotnet build` writes a fresh one, no MSBuild step or git dependency needed.
/// </summary>
internal static class BuildIdentifier
{
    public static string Value { get; } = Compute();

    private static string Compute()
    {
        var path = Assembly.GetExecutingAssembly().Location;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return "dev build";

        var builtAt = File.GetLastWriteTime(path);
        return $"built {builtAt:yyyy-MM-dd HH:mm}";
    }
}
