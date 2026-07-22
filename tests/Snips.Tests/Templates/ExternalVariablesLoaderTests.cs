using Snips.Core.Templates;

namespace Snips.Tests.Templates;

public class ExternalVariablesLoaderTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"snips-external-vars-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }

    [Fact]
    public void TryLoad_MissingFile_ReturnsNull()
    {
        Assert.Null(ExternalVariablesLoader.TryLoad(_path));
    }

    [Fact]
    public void TryLoad_ValidJson_ReturnsTheMap()
    {
        File.WriteAllText(_path, """{"companyname": "Acme AG", "supportemail": "support@acme.example"}""");

        var result = ExternalVariablesLoader.TryLoad(_path);

        Assert.NotNull(result);
        Assert.Equal("Acme AG", result["companyname"]);
        Assert.Equal("support@acme.example", result["supportemail"]);
    }

    [Fact]
    public void TryLoad_LookupIsCaseInsensitive()
    {
        File.WriteAllText(_path, """{"CompanyName": "Acme AG"}""");

        var result = ExternalVariablesLoader.TryLoad(_path);

        Assert.NotNull(result);
        Assert.True(result.TryGetValue("companyname", out var value));
        Assert.Equal("Acme AG", value);
    }

    [Fact]
    public void TryLoad_MalformedJson_ReturnsNullRatherThanThrowing()
    {
        File.WriteAllText(_path, "{ this is not valid json");

        Assert.Null(ExternalVariablesLoader.TryLoad(_path));
    }

    [Fact]
    public void TryLoad_EmptyObject_ReturnsEmptyMapNotNull()
    {
        File.WriteAllText(_path, "{}");

        var result = ExternalVariablesLoader.TryLoad(_path);

        Assert.NotNull(result);
        Assert.Empty(result);
    }
}
