using Snips.Core.Domain;
using Snips.Core.Repositories;

namespace Snips.Tests.Data;

public class SqliteShortcutRepositoryTests : IAsyncLifetime
{
    private readonly TempDatabaseFixture _fixture = new();
    private IShortcutRepository Shortcuts => _fixture.Database.Shortcuts;

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    private async Task<Snippet> CreateSnippetAsync(string name)
    {
        var now = DateTime.UtcNow;
        return await _fixture.Database.Snippets.CreateAsync(new Snippet
        {
            Id = "unused",
            Name = name,
            CreatedUtc = now,
            ModifiedUtc = now,
        });
    }

    [Fact]
    public async Task SetAsync_NewSnippet_CreatesShortcut()
    {
        var snippet = await CreateSnippetAsync("A");

        var shortcut = await Shortcuts.SetAsync(snippet.Id, modifiers: 3, virtualKey: 0x4D);

        Assert.Equal(19, shortcut.Id.Length);
        Assert.Equal(snippet.Id, shortcut.SnippetId);
        Assert.True(shortcut.IsEnabled);
    }

    [Fact]
    public async Task GetBySnippetIdAsync_ReturnsWhatWasSet()
    {
        var snippet = await CreateSnippetAsync("A");
        await Shortcuts.SetAsync(snippet.Id, 3, 0x4D);

        var fetched = await Shortcuts.GetBySnippetIdAsync(snippet.Id);

        Assert.NotNull(fetched);
        Assert.Equal(3, fetched!.Modifiers);
        Assert.Equal(0x4D, fetched.VirtualKey);
    }

    [Fact]
    public async Task SetAsync_CalledAgainForSameSnippet_OverwritesRatherThanDuplicates()
    {
        var snippet = await CreateSnippetAsync("A");
        await Shortcuts.SetAsync(snippet.Id, 3, 0x4D);
        await Shortcuts.SetAsync(snippet.Id, 3, 0x4E); // rebind to a different key

        var fetched = await Shortcuts.GetBySnippetIdAsync(snippet.Id);
        var all = await Shortcuts.ListAllAsync();

        Assert.Equal(0x4E, fetched!.VirtualKey);
        Assert.Single(all);
    }

    [Fact]
    public async Task SetAsync_ComboAlreadyUsedByAnotherSnippet_ThrowsDuplicateShortcutException()
    {
        var first = await CreateSnippetAsync("A");
        var second = await CreateSnippetAsync("B");
        await Shortcuts.SetAsync(first.Id, 3, 0x4D);

        await Assert.ThrowsAsync<DuplicateShortcutException>(() => Shortcuts.SetAsync(second.Id, 3, 0x4D));
    }

    [Fact]
    public async Task RemoveAsync_DeletesTheShortcut()
    {
        var snippet = await CreateSnippetAsync("A");
        await Shortcuts.SetAsync(snippet.Id, 3, 0x4D);

        await Shortcuts.RemoveAsync(snippet.Id);

        Assert.Null(await Shortcuts.GetBySnippetIdAsync(snippet.Id));
    }

    [Fact]
    public async Task DeletingTheSnippet_CascadesToItsShortcut()
    {
        var snippet = await CreateSnippetAsync("A");
        await Shortcuts.SetAsync(snippet.Id, 3, 0x4D);

        await _fixture.Database.Snippets.DeleteAsync(snippet.Id);

        Assert.Null(await Shortcuts.GetBySnippetIdAsync(snippet.Id));
    }

    [Fact]
    public async Task ListAllAsync_ReturnsEveryShortcut()
    {
        var a = await CreateSnippetAsync("A");
        var b = await CreateSnippetAsync("B");
        await Shortcuts.SetAsync(a.Id, 3, 0x4D);
        await Shortcuts.SetAsync(b.Id, 3, 0x4E);

        var all = await Shortcuts.ListAllAsync();

        Assert.Equal(2, all.Count);
    }
}
