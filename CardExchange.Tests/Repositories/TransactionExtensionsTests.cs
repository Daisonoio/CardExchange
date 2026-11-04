using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CardExchange.Tests.Repositories;

/// <summary>
/// Riproduce il comportamento di SQL Server con EnableRetryOnFailure: una strategia che ripete le operazioni
/// non accetta transazioni aperte "a mano" (era l'errore all'invio dei messaggi).
/// </summary>
public class TransactionExtensionsTests : IDisposable
{
    private sealed class RetryingStrategy : ExecutionStrategy
    {
        public RetryingStrategy(ExecutionStrategyDependencies deps) : base(deps, maxRetryCount: 3, maxRetryDelay: TimeSpan.FromMilliseconds(1)) { }
        protected override bool ShouldRetryOn(Exception exception) => exception is TimeoutException;
    }

    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _context;

    public TransactionExtensionsTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection, o => o.ExecutionStrategy(d => new RetryingStrategy(d)))
            .Options;
        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ManualTransaction_IsRejected_ByRetryingStrategy()
    {
        // Come faceva la chat: transazione aperta a mano, poi un salvataggio al suo interno
        var act = async () =>
        {
            await using var tx = await _context.Database.BeginTransactionAsync();
            _context.Games.Add(new Game { Name = "Manuale", Publisher = "P" });
            await _context.SaveChangesAsync();
        };
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*does not support user-initiated transactions*");
    }

    [Fact]
    public async Task InTransaction_CommitsAndReturnsResult()
    {
        var id = await _context.InTransactionAsync(async () =>
        {
            var game = new Game { Name = "Test", Publisher = "P" };
            _context.Games.Add(game);
            await _context.SaveChangesAsync();
            return game.Id;
        });

        id.Should().BeGreaterThan(0);
        (await _context.Games.CountAsync(g => g.Name == "Test")).Should().Be(1);
    }

    [Fact]
    public async Task InTransaction_OnTransientError_RetriesOnceWithoutDuplicates()
    {
        var attempts = 0;
        await _context.InTransactionAsync(async () =>
        {
            attempts++;
            _context.Games.Add(new Game { Name = "Retry", Publisher = "P" });
            await _context.SaveChangesAsync();
            if (attempts == 1) throw new TimeoutException("errore temporaneo simulato");
        });

        attempts.Should().Be(2);
        (await _context.Games.CountAsync(g => g.Name == "Retry")).Should().Be(1, "il primo tentativo è stato annullato");
    }

    [Fact]
    public async Task InTransaction_OnError_RollsBack()
    {
        var act = () => _context.InTransactionAsync(async () =>
        {
            _context.Games.Add(new Game { Name = "Rollback", Publisher = "P" });
            await _context.SaveChangesAsync();
            throw new InvalidOperationException("errore applicativo");
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
        _context.ChangeTracker.Clear();
        (await _context.Games.CountAsync(g => g.Name == "Rollback")).Should().Be(0);
    }
}
