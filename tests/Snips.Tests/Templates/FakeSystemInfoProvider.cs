using Snips.Core.Templates;

namespace Snips.Tests.Templates;

/// <summary>Fixed values so variable tests don't depend on the machine running them.</summary>
public sealed class FakeSystemInfoProvider : ISystemInfoProvider
{
    public string UserName => "roland";
    public string UserFullName => "Roland Hüttmann";
    public string MachineName => "DESKTOP-TEST";
    public string DomainName => "WORKGROUP";
    public string OsName => "Windows 11 Pro";
    public string OsVersion => "10.0.22000";
    public string IpAddress => "192.168.1.42";
    public string HomeDirectory => @"C:\Users\roland";
    public string DesktopDirectory => @"C:\Users\roland\Desktop";
    public string DocumentsDirectory => @"C:\Users\roland\Documents";
    public string DownloadsDirectory => @"C:\Users\roland\Downloads";
    public string TempDirectory => @"C:\Users\roland\AppData\Local\Temp";
    public string AppDataDirectory => @"C:\Users\roland\AppData\Roaming";
}
