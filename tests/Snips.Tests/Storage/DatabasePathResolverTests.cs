using Snips.Core.Storage;

namespace Snips.Tests.Storage;

public class DatabasePathResolverTests
{
    [Fact]
    public void PortableMarkerAbsent_ResolvesUnderLocalAppDataSnipsFolder()
    {
        var path = DatabasePathResolver.Resolve(
            exeDirectory: @"C:\Program Files\Snips",
            localAppDataDirectory: @"C:\Users\Roland\AppData\Local",
            portableMarkerExists: false);

        Assert.Equal(@"C:\Users\Roland\AppData\Local\Snips\snips.db", path);
    }

    [Fact]
    public void PortableMarkerPresent_ResolvesNextToTheExe()
    {
        var path = DatabasePathResolver.Resolve(
            exeDirectory: @"D:\USBStick\Snips",
            localAppDataDirectory: @"C:\Users\Roland\AppData\Local",
            portableMarkerExists: true);

        Assert.Equal(@"D:\USBStick\Snips\snips.db", path);
    }
}
