namespace Snips.Core.Templates;

/// <summary>Backs the §7.2 identity/system/path variables. Abstracted so tests can supply
/// fixed values instead of depending on the actual machine running the test.</summary>
public interface ISystemInfoProvider
{
    string UserName { get; }
    string UserFullName { get; }
    string MachineName { get; }
    string DomainName { get; }
    string OsName { get; }
    string OsVersion { get; }
    string IpAddress { get; }
    string HomeDirectory { get; }
    string DesktopDirectory { get; }
    string DocumentsDirectory { get; }
    string DownloadsDirectory { get; }
    string TempDirectory { get; }
    string AppDataDirectory { get; }
}
