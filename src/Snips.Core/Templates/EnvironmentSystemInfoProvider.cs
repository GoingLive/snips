using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Versioning;

namespace Snips.Core.Templates;

/// <summary>
/// Real implementation backed by plain BCL calls (Environment, Microsoft.Win32.Registry) —
/// deliberately no P/Invoke here, so this can live in Core rather than Interop, which is
/// meant to be the only project touching DllImport (SPEC.md §3.2).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class EnvironmentSystemInfoProvider : ISystemInfoProvider
{
    public static readonly EnvironmentSystemInfoProvider Instance = new();

    public string UserName => Environment.UserName;

    // .NET has no dedicated "display name" API distinct from the login name; this is the
    // best available without native interop (NetUserGetInfo etc.), which Core avoids.
    public string UserFullName => Environment.UserName;

    public string MachineName => Environment.MachineName;

    public string DomainName => Environment.UserDomainName;

    public string OsName => TryReadProductNameFromRegistry() ?? "Windows";

    public string OsVersion => Environment.OSVersion.Version.ToString();

    public string IpAddress => TryGetPrimaryIPv4Address();

    public string HomeDirectory => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public string DesktopDirectory => Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

    public string DocumentsDirectory => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    // .NET's SpecialFolder enum has no Downloads entry (there is no such CSIDL); this is the
    // near-universal default location rather than a genuine known-folder lookup.
    public string DownloadsDirectory => Path.Combine(HomeDirectory, "Downloads");

    public string TempDirectory => Path.GetTempPath();

    public string AppDataDirectory => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    private static string? TryReadProductNameFromRegistry()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            return key?.GetValue("ProductName") as string;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string TryGetPrimaryIPv4Address()
    {
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up ||
                    nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                    continue;

                foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        return addr.Address.ToString();
                }
            }
        }
        catch (Exception)
        {
            // Best-effort; an empty result is fine, a crash isn't.
        }

        return string.Empty;
    }
}
