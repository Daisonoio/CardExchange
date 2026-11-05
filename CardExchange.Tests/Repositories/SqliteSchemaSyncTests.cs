using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.Tests.Repositories;

public class SqliteSchemaSyncTests : IDisposable
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private readonly ApplicationDbContext _db;

    public SqliteSchemaSyncTests()
    {
        _conn.Open();
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    private List<string> Query(string sql)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        using var r = cmd.ExecuteReader();
        var list = new List<string>();
        while (r.Read()) list.Add(r.GetString(0));
        return list;
    }

    [Fact]
    public async Task RestoresMissingTableColumnsAndIndexes_KeepingData()
    {
        _db.Users.Add(new User { Email = "a@t.it", Username = "anna", PasswordHash = "x" });
        await _db.SaveChangesAsync();

        // Simula un database creato con una versione precedente del modello
        _db.Database.ExecuteSqlRaw("DROP TABLE \"Follows\"");
        _db.Database.ExecuteSqlRaw("DROP INDEX IF EXISTS \"IX_Stores_Slug\"");
        _db.Database.ExecuteSqlRaw("ALTER TABLE \"Stores\" DROP COLUMN \"Slug\"");
        _db.Database.ExecuteSqlRaw("ALTER TABLE \"Users\" DROP COLUMN \"TermsVersion\"");

        var result = await SqliteSchemaSync.ApplyAsync(_db);

        result.TablesCreated.Should().Contain("Follows");
        result.ColumnsAdded.Should().Contain(new[] { "Stores.Slug", "Users.TermsVersion" });
        result.Warnings.Should().BeEmpty();
        Query("SELECT name FROM sqlite_master WHERE type = 'index' AND name = 'IX_Stores_Slug'").Should().HaveCount(1);
        Query("SELECT Username FROM Users").Should().Equal("anna");

        // Il modello funziona di nuovo su tutte le tabelle toccate
        _db.ChangeTracker.Clear();
        (await _db.Users.FirstAsync()).TermsVersion.Should().BeNull();
        (await _db.Follows.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UpToDateDatabase_NothingToDo()
    {
        var result = await SqliteSchemaSync.ApplyAsync(_db);
        result.TablesCreated.Should().BeEmpty();
        result.ColumnsAdded.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }
}
