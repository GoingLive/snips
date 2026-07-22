using Snips.Core.Storage;

namespace Snips.Tests.Storage;

public class DatabasePathResolverExternalVariablesTests
{
    [Fact]
    public void ResolveExternalVariablesPath_SitsNextToTheDatabaseFile()
    {
        var path = DatabasePathResolver.ResolveExternalVariablesPath(@"C:\Users\Roland\AppData\Local\Snips\snips.db");

        Assert.Equal(@"C:\Users\Roland\AppData\Local\Snips\external-variables.json", path);
    }

    [Fact]
    public void ResolveExternalVariablesPath_PortableMode_AlsoSitsNextToTheDatabaseFile()
    {
        var path = DatabasePathResolver.ResolveExternalVariablesPath(@"D:\USBStick\Snips\snips.db");

        Assert.Equal(@"D:\USBStick\Snips\external-variables.json", path);
    }
}
