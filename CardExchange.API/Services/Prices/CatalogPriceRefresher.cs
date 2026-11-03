using System.Globalization;
using CardExchange.API.DTOs.Scryfall;
using CardExchange.Core.Entities;

namespace CardExchange.API.Services.Prices
{
    /// <summary>
    /// Aggiorna prezzi e immagini delle CardInfo dalle API dei singoli giochi.
    /// Modifica le entità passate (tracciate dal DbContext): il salvataggio è a carico del chiamante.
    /// </summary>
    public interface ICatalogPriceRefresher
    {
        /// <summary>Magic, via Scryfall /cards/collection a blocchi da 75.</summary>
        Task<int> RefreshMagicAsync(IReadOnlyList<CardInfo> cardInfos, CancellationToken ct = default);
        /// <summary>Pokémon, una richiesta per carta (l'API non ha un endpoint batch).</summary>
        Task<int> RefreshPokemonAsync(IReadOnlyList<CardInfo> cardInfos, CancellationToken ct = default);
        /// <summary>Yu-Gi-Oh!, una richiesta per carta.</summary>
        Task<int> RefreshYuGiOhAsync(IReadOnlyList<CardInfo> cardInfos, CancellationToken ct = default);
    }

    public class CatalogPriceRefresher : ICatalogPriceRefresher
    {
        private readonly IScryfallService _scryfall;
        private readonly IPokemonTcgService _pokemon;
        private readonly IYuGiOhService _yugioh;
        private readonly ILogger<CatalogPriceRefresher> _logger;

        public CatalogPriceRefresher(IScryfallService scryfall, IPokemonTcgService pokemon, IYuGiOhService yugioh, ILogger<CatalogPriceRefresher> logger)
        {
            _scryfall = scryfall;
            _pokemon = pokemon;
            _yugioh = yugioh;
            _logger = logger;
        }

        public async Task<int> RefreshMagicAsync(IReadOnlyList<CardInfo> cardInfos, CancellationToken ct = default)
        {
            int updated = 0;
            foreach (var batch in cardInfos.Where(ci => ci.ScryfallId != null).Chunk(75))
            {
                ct.ThrowIfCancellationRequested();
                var result = await _scryfall.ValidateCollectionAsync(
                    batch.Select(ci => new ScryfallIdentifier { Id = ci.ScryfallId }).ToList());
                if (result == null) continue;

                var byId = batch.ToDictionary(ci => ci.ScryfallId!, StringComparer.OrdinalIgnoreCase);
                foreach (var card in result.Data)
                {
                    if (!byId.TryGetValue(card.Id, out var cardInfo)) continue;

                    if (card.Prices != null)
                    {
                        cardInfo.PriceUsd = ParseDecimal(card.Prices.Usd);
                        cardInfo.PriceUsdFoil = ParseDecimal(card.Prices.UsdFoil);
                        cardInfo.PriceEur = ParseDecimal(card.Prices.Eur);
                        cardInfo.PriceEurFoil = ParseDecimal(card.Prices.EurFoil);
                    }

                    var images = card.ImageUris ?? card.CardFaces?.FirstOrDefault()?.ImageUris;
                    if (images != null)
                    {
                        cardInfo.ImageUrl = images.Normal;
                        cardInfo.ImageSmall = images.Small;
                        cardInfo.ImageNormal = images.Normal;
                        cardInfo.ImageLarge = images.Large;
                    }

                    cardInfo.ScryfallUpdatedAt = DateTime.UtcNow;
                    updated++;
                }
            }
            return updated;
        }

        public async Task<int> RefreshPokemonAsync(IReadOnlyList<CardInfo> cardInfos, CancellationToken ct = default)
        {
            int updated = 0;
            foreach (var cardInfo in cardInfos.Where(ci => ci.PokemonTcgId != null))
            {
                ct.ThrowIfCancellationRequested();
                var apiCard = await _pokemon.GetCardByIdAsync(cardInfo.PokemonTcgId!);
                if (apiCard == null) continue;

                if (apiCard.TcgPlayer?.Prices != null)
                {
                    cardInfo.PriceTcgNormal = apiCard.TcgPlayer.Prices.Normal?.Market;
                    cardInfo.PriceTcgHolofoil = apiCard.TcgPlayer.Prices.Holofoil?.Market;
                    cardInfo.PriceTcgReverseHolofoil = apiCard.TcgPlayer.Prices.ReverseHolofoil?.Market;
                    cardInfo.PriceUsd = apiCard.TcgPlayer.Prices.Normal?.Market
                        ?? apiCard.TcgPlayer.Prices.Holofoil?.Market
                        ?? apiCard.TcgPlayer.Prices.ReverseHolofoil?.Market;
                }

                if (apiCard.Cardmarket?.Prices != null)
                {
                    cardInfo.PriceCardmarketAvg = apiCard.Cardmarket.Prices.AverageSellPrice;
                    cardInfo.PriceCardmarketTrend = apiCard.Cardmarket.Prices.TrendPrice;
                    cardInfo.PriceEur = apiCard.Cardmarket.Prices.AverageSellPrice
                        ?? apiCard.Cardmarket.Prices.TrendPrice;
                }

                if (apiCard.Images != null)
                {
                    cardInfo.PokemonImageSmall = apiCard.Images.Small;
                    cardInfo.PokemonImageLarge = apiCard.Images.Large;
                }

                cardInfo.PokemonTcgUpdatedAt = DateTime.UtcNow;
                updated++;
            }
            return updated;
        }

        public async Task<int> RefreshYuGiOhAsync(IReadOnlyList<CardInfo> cardInfos, CancellationToken ct = default)
        {
            int updated = 0;
            foreach (var cardInfo in cardInfos.Where(ci => ci.YuGiOhId != null))
            {
                ct.ThrowIfCancellationRequested();
                var apiCard = await _yugioh.GetCardByIdAsync(cardInfo.YuGiOhId!.Value);
                if (apiCard == null) continue;

                var prices = apiCard.CardPrices?.FirstOrDefault();
                if (prices != null)
                {
                    cardInfo.PriceYuGiOhTcgPlayer = ParseDecimal(prices.TcgPlayerPrice);
                    cardInfo.PriceYuGiOhCardmarket = ParseDecimal(prices.CardmarketPrice);
                    cardInfo.PriceYuGiOhEbay = ParseDecimal(prices.EbayPrice);
                    cardInfo.PriceYuGiOhAmazon = ParseDecimal(prices.AmazonPrice);
                    cardInfo.PriceYuGiOhCoolstuffinc = ParseDecimal(prices.CoolStuffIncPrice);
                    cardInfo.PriceUsd = ParseDecimal(prices.TcgPlayerPrice);
                    cardInfo.PriceEur = ParseDecimal(prices.CardmarketPrice);
                }

                var image = apiCard.CardImages?.FirstOrDefault();
                if (image != null)
                {
                    cardInfo.YuGiOhImageUrl = image.ImageUrl;
                    cardInfo.YuGiOhImageSmall = image.ImageUrlSmall;
                }

                cardInfo.YuGiOhUpdatedAt = DateTime.UtcNow;
                updated++;
            }
            return updated;
        }

        private static decimal? ParseDecimal(string? value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) && result > 0 ? result : null;
        }
    }
}
