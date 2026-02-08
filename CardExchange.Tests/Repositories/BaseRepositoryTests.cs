using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using CardExchange.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.Tests.Repositories;

public class BaseRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly BaseRepository<Game> _repository;

    public BaseRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _repository = new BaseRepository<Game>(_context);
    }

    [Fact]
    public async Task AddAsync_ShouldAddEntity()
    {
        // Arrange
        var game = new Game
        {
            Name = "Test Game",
            Publisher = "Test Publisher",
            Description = "Test Description"
        };

        // Act
        var result = await _repository.AddAsync(game);
        await _repository.SaveChangesAsync();

        // Assert
        result.Should().NotBeNull();
        var saved = await _context.Games.FirstOrDefaultAsync(g => g.Name == "Test Game");
        saved.Should().NotBeNull();
        saved!.Publisher.Should().Be("Test Publisher");
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsEntity()
    {
        // Arrange
        var game = new Game { Name = "Find Me", Publisher = "Publisher" };
        _context.Games.Add(game);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(game.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Find Me");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingId_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllEntities()
    {
        // Arrange
        _context.Games.AddRange(
            new Game { Name = "Game 1", Publisher = "P1" },
            new Game { Name = "Game 2", Publisher = "P2" },
            new Game { Name = "Game 3", Publisher = "P3" }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task FindAsync_WithPredicate_ReturnsMatchingEntities()
    {
        // Arrange
        _context.Games.AddRange(
            new Game { Name = "Magic", Publisher = "Wizards" },
            new Game { Name = "Pokemon", Publisher = "Nintendo" },
            new Game { Name = "Yu-Gi-Oh", Publisher = "Konami" }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.FindAsync(g => g.Publisher == "Nintendo");

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("Pokemon");
    }

    [Fact]
    public async Task Delete_SoftDeletesEntity()
    {
        // Arrange
        var game = new Game { Name = "To Delete", Publisher = "Publisher" };
        _context.Games.Add(game);
        await _context.SaveChangesAsync();

        // Act
        _repository.Delete(game);
        await _repository.SaveChangesAsync();

        // Assert - Soft delete: entity exists but IsDeleted = true
        var deleted = await _context.Games.IgnoreQueryFilters()
            .FirstOrDefaultAsync(g => g.Name == "To Delete");
        deleted.Should().NotBeNull();
        deleted!.IsDeleted.Should().BeTrue();

        // Should not be visible through normal query (query filter)
        var normal = await _context.Games.FirstOrDefaultAsync(g => g.Name == "To Delete");
        normal.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_WithExistingEntity_ReturnsTrue()
    {
        // Arrange
        _context.Games.Add(new Game { Name = "Existing", Publisher = "P" });
        await _context.SaveChangesAsync();

        // Act
        var exists = await _repository.ExistsAsync(g => g.Name == "Existing");

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithNonExisting_ReturnsFalse()
    {
        // Act
        var exists = await _repository.ExistsAsync(g => g.Name == "NonExisting");

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task CountAsync_ReturnsCorrectCount()
    {
        // Arrange
        _context.Games.AddRange(
            new Game { Name = "G1", Publisher = "P" },
            new Game { Name = "G2", Publisher = "P" }
        );
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.CountAsync();

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsCorrectPage()
    {
        // Arrange
        for (int i = 1; i <= 10; i++)
        {
            _context.Games.Add(new Game { Name = $"Game {i}", Publisher = "P" });
        }
        await _context.SaveChangesAsync();

        // Act
        var (items, totalCount) = await _repository.GetPagedAsync(page: 2, pageSize: 3);

        // Assert
        totalCount.Should().Be(10);
        items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Update_ShouldUpdateEntity()
    {
        // Arrange
        var game = new Game { Name = "Original", Publisher = "P" };
        _context.Games.Add(game);
        await _context.SaveChangesAsync();

        // Act
        game.Name = "Updated";
        _repository.Update(game);
        await _repository.SaveChangesAsync();

        // Assert
        var updated = await _context.Games.FindAsync(game.Id);
        updated!.Name.Should().Be("Updated");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
