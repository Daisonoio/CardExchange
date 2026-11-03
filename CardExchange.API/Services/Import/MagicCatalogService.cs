using CardExchange.API.DTOs.Scryfall;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services.Import
{
    public interface IMagicCatalogService
    {
        /// <summary>
        /// Restituisce la CardInfo locale per una carta Scryfall, creandola (con gioco e set) se manca.
        /// Se esiste già ne aggiorna dati e prezzi.
        /// </summary>
        Task<(CardInfo CardInfo, bool IsNew)> GetOrCreateCardInfoAsync(ScryfallCard scryfallCard);
    }

    public class MagicCatalogService : IMagicCatalogService
    {
        public const string MagicGameName = "Magic: The Gathering";

        private readonly ApplicationDbContext _context;
        private readonly IScryfallService _scryfall;
        private readonly ILogger<MagicCatalogService> _logger;
        private Game? _game;
        private readonly Dictionary<string, CardSet> _setCache = new(StringComparer.OrdinalIgnoreCase);

        public MagicCatalogService(ApplicationDbContext context, IScryfallService scryfall, ILogger<MagicCatalogService> logger)
        {
            _context = context;
            _scryfall = scryfall;
            _logger = logger;
        }

        public async Task<(CardInfo CardInfo, bool IsNew)> GetOrCreateCardInfoAsync(ScryfallCard scryfallCard)
        {
            var existing = await _context.CardInfos.FirstOrDefaultAsync(ci => ci.ScryfallId == scryfallCard.Id);
            if (existing != null)
            {
                _scryfall.MapScryfallToCardInfo(scryfallCard, existing);
                await _context.SaveChangesAsync();
                return (existing, false);
            }

            var cardSet = await GetOrCreateSetAsync(scryfallCard);
            var cardInfo = new CardInfo { CardSetId = cardSet.Id };
            _scryfall.MapScryfallToCardInfo(scryfallCard, cardInfo);
            _context.CardInfos.Add(cardInfo);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Carta importata da Scryfall: {CardName} ({SetCode} #{Number}) -> CardInfo {CardInfoId}",
                scryfallCard.Name, scryfallCard.SetCode, scryfallCard.CollectorNumber, cardInfo.Id);
            return (cardInfo, true);
        }

        private async Task<Game> GetOrCreateGameAsync()
        {
            if (_game != null) return _game;
            _game = await _context.Games.FirstOrDefaultAsync(g => g.Name == MagicGameName);
            if (_game == null)
            {
                _game = new Game
                {
                    Name = MagicGameName,
                    Publisher = "Wizards of the Coast",
                    Description = "Il gioco di carte collezionabili più famoso al mondo"
                };
                _context.Games.Add(_game);
                await _context.SaveChangesAsync();
            }
            return _game;
        }

        private async Task<CardSet> GetOrCreateSetAsync(ScryfallCard scryfallCard)
        {
            var setCode = scryfallCard.SetCode.ToUpperInvariant();
            if (_setCache.TryGetValue(setCode, out var cached)) return cached;

            var game = await GetOrCreateGameAsync();
            var cardSet = await _context.CardSets.FirstOrDefaultAsync(cs => cs.Code == setCode && cs.GameId == game.Id);
            if (cardSet == null)
            {
                var scryfallSet = await _scryfall.GetSetByCodeAsync(scryfallCard.SetCode);
                cardSet = new CardSet { GameId = game.Id };
                if (scryfallSet != null)
                {
                    _scryfall.MapScryfallToCardSet(scryfallSet, cardSet);
                }
                else
                {
                    cardSet.Code = setCode;
                    cardSet.Name = scryfallCard.SetName;
                }
                _context.CardSets.Add(cardSet);
                await _context.SaveChangesAsync();
            }

            _setCache[setCode] = cardSet;
            return cardSet;
        }
    }
}
