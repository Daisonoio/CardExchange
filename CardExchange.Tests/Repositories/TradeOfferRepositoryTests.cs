using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using CardExchange.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.Tests.Repositories;

public class TradeOfferRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly TradeOfferRepository _repository;

    public TradeOfferRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _repository = new TradeOfferRepository(_context);

        SeedData().Wait();
    }

    private async Task SeedData()
    {
        var sender = new User { Id = 1, Email = "sender@test.com", Username = "sender", FirstName = "S", LastName = "S", PasswordHash = "h" };
        var receiver = new User { Id = 2, Email = "receiver@test.com", Username = "receiver", FirstName = "R", LastName = "R", PasswordHash = "h" };
        _context.Users.AddRange(sender, receiver);

        var game = new Game { Id = 1, Name = "Pokemon", Publisher = "Nintendo" };
        _context.Games.Add(game);

        var cardSet = new CardSet { Id = 1, GameId = 1, Name = "Base Set" };
        _context.CardSets.Add(cardSet);

        var cardInfo = new CardInfo { Id = 1, CardSetId = 1, Name = "Pikachu" };
        _context.CardInfos.Add(cardInfo);

        var card1 = new Card { Id = 1, UserId = 1, CardInfoId = 1, Condition = CardCondition.NearMint, IsAvailableForTrade = true };
        var card2 = new Card { Id = 2, UserId = 2, CardInfoId = 1, Condition = CardCondition.Good, IsAvailableForTrade = true };
        _context.Cards.AddRange(card1, card2);

        var offer1 = new TradeOffer { Id = 1, SenderId = 1, ReceiverId = 2, Status = TradeOfferStatus.Pending };
        var offer2 = new TradeOffer { Id = 2, SenderId = 1, ReceiverId = 2, Status = TradeOfferStatus.Accepted };
        var offer3 = new TradeOffer { Id = 3, SenderId = 2, ReceiverId = 1, Status = TradeOfferStatus.Pending };
        _context.TradeOffers.AddRange(offer1, offer2, offer3);

        offer1.Items.Add(new TradeOfferItem { CardId = 1, Side = TradeOfferItemSide.Offered, Quantity = 1 });
        offer1.Items.Add(new TradeOfferItem { CardId = 2, Side = TradeOfferItemSide.Requested, Quantity = 1 });

        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetActiveOfferCountAsync_ReturnsCorrectCount()
    {
        var count = await _repository.GetActiveOfferCountAsync(1);
        count.Should().Be(1); // Only offer1 is Pending from user 1
    }

    [Fact]
    public async Task GetUserOffersAsync_ReturnsAllUserOffers()
    {
        var offers = await _repository.GetUserOffersAsync(1);
        offers.Should().HaveCount(3); // All 3 offers involve user 1
    }

    [Fact]
    public async Task GetUserOffersAsync_FilterByStatus_ReturnsFiltered()
    {
        var offers = await _repository.GetUserOffersAsync(1, TradeOfferStatus.Pending);
        offers.Should().HaveCount(2); // offer1 (sent) + offer3 (received)
    }

    [Fact]
    public async Task GetWithDetailsAsync_ReturnsFullGraph()
    {
        var offer = await _repository.GetWithDetailsAsync(1);

        offer.Should().NotBeNull();
        offer!.Sender.Should().NotBeNull();
        offer.Receiver.Should().NotBeNull();
        offer.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetUserOffersPagedAsync_ReturnsCorrectPage()
    {
        var (items, totalCount) = await _repository.GetUserOffersPagedAsync(1, 1, 2);

        totalCount.Should().Be(3);
        items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetWithDetailsAsync_NonExistentId_ReturnsNull()
    {
        var offer = await _repository.GetWithDetailsAsync(999);
        offer.Should().BeNull();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
