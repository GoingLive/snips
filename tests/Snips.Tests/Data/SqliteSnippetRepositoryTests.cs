using Snips.Core.Domain;
using Snips.Core.Repositories;

namespace Snips.Tests.Data;

public class SqliteSnippetRepositoryTests : IAsyncLifetime
{
    private readonly TempDatabaseFixture _fixture = new();
    private ISnippetRepository Repository => _fixture.Database.Snippets;

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    private static Snippet NewDraft(string name = "Meeting follow-up") => new()
    {
        Id = "unused",
        Name = name,
        Description = "Sent after a client call",
        BodyHtml = "<p>Dear {{input:Name}},</p>",
        PlainText = "Dear {{input:Name}},",
        CreatedUtc = default,
        ModifiedUtc = default,
    };

    [Fact]
    public async Task CreateAsync_AssignsA19CharacterId_AndPersistsAllFields()
    {
        var created = await Repository.CreateAsync(NewDraft());

        Assert.Equal(19, created.Id.Length);
        Assert.True(long.TryParse(created.Id, out _));
        Assert.NotEqual(default, created.CreatedUtc);
        Assert.Equal(created.CreatedUtc, created.ModifiedUtc);

        var fetched = await Repository.GetByIdAsync(created.Id);
        Assert.NotNull(fetched);
        Assert.Equal("Meeting follow-up", fetched!.Name);
        Assert.Equal("Sent after a client call", fetched.Description);
        Assert.Equal("<p>Dear {{input:Name}},</p>", fetched.BodyHtml);
        Assert.True(fetched.IsRichText);
    }

    [Fact]
    public async Task CreateAsync_DuplicateNameCaseInsensitive_ThrowsDuplicateSnippetNameException()
    {
        await Repository.CreateAsync(NewDraft("Invoice header"));

        var ex = await Assert.ThrowsAsync<DuplicateSnippetNameException>(
            () => Repository.CreateAsync(NewDraft("INVOICE HEADER")));

        Assert.Equal("INVOICE HEADER", ex.Name);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        Assert.Null(await Repository.GetByIdAsync("0000000000000000000"));
    }

    [Fact]
    public async Task GetByNameAsync_IsCaseInsensitive()
    {
        await Repository.CreateAsync(NewDraft("Bug report template"));

        var found = await Repository.GetByNameAsync("bug REPORT template");

        Assert.NotNull(found);
        Assert.Equal("Bug report template", found!.Name);
    }

    [Fact]
    public async Task UpdateAsync_ChangesFieldsAndBumpsModifiedUtc()
    {
        var created = await Repository.CreateAsync(NewDraft());
        var originalModified = created.ModifiedUtc;

        created.Description = "Updated description";
        await Task.Delay(10); // ensure the clock actually advances
        await Repository.UpdateAsync(created);

        var fetched = await Repository.GetByIdAsync(created.Id);
        Assert.Equal("Updated description", fetched!.Description);
        Assert.True(fetched.ModifiedUtc > originalModified);
        Assert.Equal(created.CreatedUtc, fetched.CreatedUtc); // CreatedUtc must not change
    }

    [Fact]
    public async Task UpdateAsync_RenamingToAnExistingName_ThrowsAndLeavesOriginalUntouched()
    {
        await Repository.CreateAsync(NewDraft("Signature (formal)"));
        var second = await Repository.CreateAsync(NewDraft("Signature (casual)"));

        second.Name = "Signature (formal)";
        await Assert.ThrowsAsync<DuplicateSnippetNameException>(() => Repository.UpdateAsync(second));

        var stillCasual = await Repository.GetByIdAsync(second.Id);
        Assert.Equal("Signature (casual)", stillCasual!.Name);
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ThrowsKeyNotFoundException()
    {
        var ghost = NewDraft();
        ghost.Id = "9999999999999999999";

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Repository.UpdateAsync(ghost));
    }

    [Fact]
    public async Task RecordUseAsync_IncrementsUseCountAndSetsLastUsedUtc()
    {
        var created = await Repository.CreateAsync(NewDraft());
        Assert.Null(created.LastUsedUtc);

        await Repository.RecordUseAsync(created.Id);
        await Repository.RecordUseAsync(created.Id);

        var fetched = await Repository.GetByIdAsync(created.Id);
        Assert.Equal(2, fetched!.UseCount);
        Assert.NotNull(fetched.LastUsedUtc);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheSnippet_AndReturnsFalseOnSecondDelete()
    {
        var created = await Repository.CreateAsync(NewDraft());

        Assert.True(await Repository.DeleteAsync(created.Id));
        Assert.Null(await Repository.GetByIdAsync(created.Id));
        Assert.False(await Repository.DeleteAsync(created.Id));
    }

    [Fact]
    public async Task ListAsync_OrdersMostRecentlyUsedFirst_ThenNeverUsedByNameAlphabetically()
    {
        var zebra = await Repository.CreateAsync(NewDraft("Zebra"));
        var alpha = await Repository.CreateAsync(NewDraft("Alpha"));
        var recentlyUsed = await Repository.CreateAsync(NewDraft("Recently used"));

        await Repository.RecordUseAsync(recentlyUsed.Id);

        var list = await Repository.ListAsync();
        var names = list.Select(s => s.Name).ToList();

        Assert.Equal(["Recently used", "Alpha", "Zebra"], names);
    }

    // --- Favorites (Roland, 2026-07-24) ------------------------------------------------------

    [Fact]
    public async Task CreateAsync_DefaultsToNotFavoriteWithZeroSortOrder()
    {
        var created = await Repository.CreateAsync(NewDraft());

        Assert.False(created.IsFavorite);
        Assert.Equal(0, created.FavoriteSortOrder);
    }

    [Fact]
    public async Task IsFavoriteAndFavoriteSortOrder_RoundTripThroughCreateAndGet()
    {
        var draft = NewDraft();
        draft.IsFavorite = true;
        draft.FavoriteSortOrder = 3;

        var created = await Repository.CreateAsync(draft);
        var fetched = await Repository.GetByIdAsync(created.Id);

        Assert.True(fetched!.IsFavorite);
        Assert.Equal(3, fetched.FavoriteSortOrder);
    }

    [Fact]
    public async Task IsFavoriteAndFavoriteSortOrder_RoundTripThroughUpdate()
    {
        var created = await Repository.CreateAsync(NewDraft());

        created.IsFavorite = true;
        created.FavoriteSortOrder = 7;
        await Repository.UpdateAsync(created);

        var fetched = await Repository.GetByIdAsync(created.Id);
        Assert.True(fetched!.IsFavorite);
        Assert.Equal(7, fetched.FavoriteSortOrder);
    }
}
