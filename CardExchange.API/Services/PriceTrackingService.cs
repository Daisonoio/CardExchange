using CardExchange.Core.Entities;
using CardExchange.Core.Interfaces;

namespace CardExchange.API.Services
{
    public class PriceTrackingService : IPriceTrackingService
    {
        private readonly ICardRepository _cardRepository;
        private readonly ICardInfoRepository _cardInfoRepository;
        private readonly IPriceHistoryRepository _priceHistoryRepository;
        private readonly IBaseRepository<PriceAlert> _priceAlertRepository;
        private readonly INotificationService _notificationService;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<PriceTrackingService> _logger;

        public PriceTrackingService(
            ICardRepository cardRepository,
            ICardInfoRepository cardInfoRepository,
            IPriceHistoryRepository priceHistoryRepository,
            IBaseRepository<PriceAlert> priceAlertRepository,
            INotificationService notificationService,
            IUserRepository userRepository,
            ILogger<PriceTrackingService> logger)
        {
            _cardRepository = cardRepository;
            _cardInfoRepository = cardInfoRepository;
            _priceHistoryRepository = priceHistoryRepository;
            _priceAlertRepository = priceAlertRepository;
            _notificationService = notificationService;
            _userRepository = userRepository;
            _logger = logger;
        }

        public async Task<PortfolioSummary> GetPortfolioSummaryAsync(int userId)
        {
            var cards = await _cardRepository.GetUserCardsAsync(userId);
            var cardList = cards.ToList();

            if (!cardList.Any())
            {
                return new PortfolioSummary();
            }

            // Calcola valore totale
            decimal totalEur = 0, totalUsd = 0;
            CardValueInfo? mostValuable = null;
            decimal highestValue = 0;

            foreach (var card in cardList)
            {
                var info = card.CardInfo;
                if (info == null) continue;

                var eurValue = (info.PriceEur ?? 0) * card.Quantity;
                var usdValue = (info.PriceUsd ?? 0) * card.Quantity;
                totalEur += eurValue;
                totalUsd += usdValue;

                if (eurValue > highestValue)
                {
                    highestValue = eurValue;
                    mostValuable = new CardValueInfo
                    {
                        CardInfoId = info.Id,
                        Name = info.Name,
                        SetName = info.CardSet?.Name,
                        PriceEur = info.PriceEur,
                        PriceUsd = info.PriceUsd,
                        ImageSmall = info.ImageSmall
                    };
                }
            }

            // Calcola variazione 24h
            var cardInfoIds = cardList
                .Where(c => c.CardInfo != null)
                .Select(c => c.CardInfoId)
                .Distinct()
                .ToList();

            var yesterday = DateTime.UtcNow.Date.AddDays(-1);
            decimal previousTotalEur = 0;

            var previousSnapshots = await _priceHistoryRepository.GetLatestSnapshotsAsync(cardInfoIds);
            var snapshotDict = previousSnapshots.ToDictionary(s => s.CardInfoId);

            foreach (var card in cardList)
            {
                if (snapshotDict.TryGetValue(card.CardInfoId, out var snapshot))
                {
                    previousTotalEur += (snapshot.PriceEur ?? 0) * card.Quantity;
                }
            }

            var changeEur = totalEur - previousTotalEur;
            var changePct = previousTotalEur > 0 ? (changeEur / previousTotalEur) * 100 : 0;

            return new PortfolioSummary
            {
                TotalValueEur = totalEur,
                TotalValueUsd = totalUsd,
                ChangeEur24h = changeEur,
                ChangeUsd24h = 0, // Semplificato
                ChangePercentage24h = Math.Round(changePct, 2),
                TotalCards = cardList.Sum(c => c.Quantity),
                UniqueCards = cardList.Count,
                MostValuableCard = mostValuable
            };
        }

        public async Task<IEnumerable<PortfolioMovers>> GetPortfolioMoversAsync(int userId, int limit = 10)
        {
            var cards = await _cardRepository.GetUserCardsAsync(userId);
            var cardList = cards.ToList();

            if (!cardList.Any()) return Enumerable.Empty<PortfolioMovers>();

            var cardInfoIds = cardList
                .Where(c => c.CardInfo != null)
                .Select(c => c.CardInfoId)
                .Distinct()
                .ToList();

            var snapshots = await _priceHistoryRepository.GetLatestSnapshotsAsync(cardInfoIds);
            var snapshotDict = snapshots.ToDictionary(s => s.CardInfoId);

            var movers = new List<PortfolioMovers>();

            foreach (var card in cardList)
            {
                var info = card.CardInfo;
                if (info?.PriceEur == null) continue;

                if (snapshotDict.TryGetValue(card.CardInfoId, out var snapshot) && snapshot.PriceEur.HasValue)
                {
                    var change = info.PriceEur.Value - snapshot.PriceEur.Value;
                    var pct = snapshot.PriceEur.Value > 0
                        ? (change / snapshot.PriceEur.Value) * 100
                        : 0;

                    if (change != 0)
                    {
                        movers.Add(new PortfolioMovers
                        {
                            CardInfoId = info.Id,
                            Name = info.Name,
                            SetName = info.CardSet?.Name,
                            CurrentPrice = info.PriceEur,
                            PreviousPrice = snapshot.PriceEur,
                            ChangeAmount = change,
                            ChangePercentage = Math.Round(pct, 2),
                            ImageSmall = info.ImageSmall
                        });
                    }
                }
            }

            return movers
                .OrderByDescending(m => Math.Abs(m.ChangePercentage))
                .Take(limit);
        }

        public async Task<IEnumerable<PriceHistory>> GetPriceHistoryAsync(int cardInfoId, int days = 90)
        {
            return await _priceHistoryRepository.GetHistoryForCardAsync(cardInfoId, days);
        }

        public async Task SnapshotPricesForUserAsync(int userId)
        {
            var cards = await _cardRepository.GetUserCardsAsync(userId);
            var today = DateTime.UtcNow.Date;

            foreach (var card in cards)
            {
                var info = card.CardInfo;
                if (info == null) continue;

                var alreadySnapped = await _priceHistoryRepository.HasSnapshotForDateAsync(info.Id, today);
                if (alreadySnapped) continue;

                await _priceHistoryRepository.SaveSnapshotAsync(
                    info.Id, info.PriceUsd, info.PriceUsdFoil, info.PriceEur, info.PriceEurFoil);
            }
        }

        public async Task<IEnumerable<PriceAlert>> GetUserAlertsAsync(int userId)
        {
            return await _priceAlertRepository.FindAsync(a => a.UserId == userId && !a.IsDeleted);
        }

        public async Task<PriceAlert> CreateAlertAsync(PriceAlert alert)
        {
            await _priceAlertRepository.AddAsync(alert);
            await _priceAlertRepository.SaveChangesAsync();
            return alert;
        }

        public async Task<bool> DeleteAlertAsync(int alertId, int userId)
        {
            var alert = await _priceAlertRepository.GetByIdAsync(alertId);
            if (alert == null || alert.UserId != userId) return false;

            _priceAlertRepository.Delete(alert);
            return await _priceAlertRepository.SaveChangesAsync();
        }

        public async Task CheckAndTriggerAlertsAsync(int userId)
        {
            var alerts = await _priceAlertRepository.FindAsync(
                a => a.UserId == userId && a.IsActive && !a.IsTriggered);

            foreach (var alert in alerts)
            {
                var cardInfo = await _cardInfoRepository.GetByIdAsync(alert.CardInfoId);
                if (cardInfo == null) continue;

                var currentPrice = alert.Currency == AlertCurrency.Eur
                    ? cardInfo.PriceEur
                    : cardInfo.PriceUsd;

                if (!currentPrice.HasValue) continue;

                bool triggered = alert.Direction == AlertDirection.Above
                    ? currentPrice.Value >= alert.TargetPrice
                    : currentPrice.Value <= alert.TargetPrice;

                if (triggered)
                {
                    alert.IsTriggered = true;
                    alert.TriggeredAt = DateTime.UtcNow;
                    _priceAlertRepository.Update(alert);
                    await _priceAlertRepository.SaveChangesAsync();

                    var direction = alert.Direction == AlertDirection.Above ? "salito sopra" : "sceso sotto";
                    var currency = alert.Currency == AlertCurrency.Eur ? "EUR" : "USD";
                    await _notificationService.SendAsync(
                        userId,
                        NotificationType.SystemAnnouncement,
                        $"Allarme prezzo: {cardInfo.Name}",
                        $"Il prezzo di {cardInfo.Name} è {direction} {alert.TargetPrice:F2} {currency}. Prezzo attuale: {currentPrice.Value:F2} {currency}",
                        alert.CardInfoId,
                        "PriceAlert");
                }
            }
        }

        public async Task<TradeAnalysis> AnalyzeTradeAsync(IEnumerable<int> offeredCardIds, IEnumerable<int> requestedCardIds)
        {
            var offeredCards = new List<TradeCardValue>();
            var requestedCards = new List<TradeCardValue>();
            decimal offeredEur = 0, offeredUsd = 0, requestedEur = 0, requestedUsd = 0;

            foreach (var cardId in offeredCardIds)
            {
                var card = await _cardRepository.GetByIdAsync(cardId);
                if (card == null) continue;

                var info = await _cardInfoRepository.GetByIdAsync(card.CardInfoId);
                if (info == null) continue;

                offeredEur += info.PriceEur ?? 0;
                offeredUsd += info.PriceUsd ?? 0;
                offeredCards.Add(new TradeCardValue
                {
                    CardId = card.Id,
                    Name = info.Name,
                    PriceEur = info.PriceEur,
                    PriceUsd = info.PriceUsd,
                    ImageSmall = info.ImageSmall
                });
            }

            foreach (var cardId in requestedCardIds)
            {
                var card = await _cardRepository.GetByIdAsync(cardId);
                if (card == null) continue;

                var info = await _cardInfoRepository.GetByIdAsync(card.CardInfoId);
                if (info == null) continue;

                requestedEur += info.PriceEur ?? 0;
                requestedUsd += info.PriceUsd ?? 0;
                requestedCards.Add(new TradeCardValue
                {
                    CardId = card.Id,
                    Name = info.Name,
                    PriceEur = info.PriceEur,
                    PriceUsd = info.PriceUsd,
                    ImageSmall = info.ImageSmall
                });
            }

            var differenceEur = offeredEur - requestedEur;
            var threshold = Math.Max(offeredEur, requestedEur) * 0.10m; // 10% soglia

            string verdict;
            if (Math.Abs(differenceEur) <= threshold)
                verdict = "fair";
            else if (differenceEur > 0)
                verdict = "against_you";
            else
                verdict = "in_your_favor";

            return new TradeAnalysis
            {
                OfferedValueEur = offeredEur,
                OfferedValueUsd = offeredUsd,
                RequestedValueEur = requestedEur,
                RequestedValueUsd = requestedUsd,
                DifferenceEur = differenceEur,
                DifferenceUsd = offeredUsd - requestedUsd,
                Verdict = verdict,
                OfferedCards = offeredCards,
                RequestedCards = requestedCards
            };
        }

        public async Task<IEnumerable<PriceSpikeInfo>> DetectPriceSpikesAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            var threshold = user?.PriceSpikeThreshold ?? 10m;

            var cards = await _cardRepository.GetUserCardsAsync(userId);
            var cardList = cards.ToList();
            if (!cardList.Any()) return Enumerable.Empty<PriceSpikeInfo>();

            var cardInfoIds = cardList
                .Where(c => c.CardInfo != null)
                .Select(c => c.CardInfoId)
                .Distinct()
                .ToList();

            // Get last 5 days of price history for all cards
            var history = await _priceHistoryRepository.GetHistoryForCardsAsync(cardInfoIds, 5);
            var historyByCard = history.GroupBy(h => h.CardInfoId).ToDictionary(g => g.Key, g => g.OrderBy(h => h.SnapshotDate).ToList());

            var spikes = new List<PriceSpikeInfo>();

            foreach (var card in cardList)
            {
                var info = card.CardInfo;
                if (info?.PriceEur == null || info.PriceEur <= 0) continue;

                if (!historyByCard.TryGetValue(card.CardInfoId, out var cardHistory) || !cardHistory.Any())
                    continue;

                // Compare current price with the oldest available snapshot in last 5 days
                var oldestSnapshot = cardHistory.First();
                if (oldestSnapshot.PriceEur == null || oldestSnapshot.PriceEur <= 0) continue;

                var changeAmount = info.PriceEur.Value - oldestSnapshot.PriceEur.Value;
                var changePct = (changeAmount / oldestSnapshot.PriceEur.Value) * 100;

                // Only report upward spikes above threshold
                if (changePct >= threshold)
                {
                    spikes.Add(new PriceSpikeInfo
                    {
                        CardId = card.Id,
                        CardInfoId = info.Id,
                        Name = info.Name,
                        SetName = info.CardSet?.Name,
                        ImageSmall = info.ImageSmall,
                        CurrentPriceEur = info.PriceEur.Value,
                        OldPriceEur = oldestSnapshot.PriceEur.Value,
                        ChangePercentage = Math.Round(changePct, 2),
                        ChangeAmount = Math.Round(changeAmount, 2),
                        Last5Days = cardHistory.Select(h => new PriceDayPoint
                        {
                            Date = h.SnapshotDate,
                            PriceEur = h.PriceEur
                        }).ToList()
                    });
                }
            }

            return spikes.OrderByDescending(s => s.ChangePercentage);
        }

        public async Task<int> CheckAndNotifySpikesAsync(int userId)
        {
            var spikes = (await DetectPriceSpikesAsync(userId)).ToList();
            if (!spikes.Any()) return 0;

            await _notificationService.SendAsync(
                userId,
                NotificationType.PriceSpike,
                "Hai delle carte che stanno salendo di prezzo!",
                $"{spikes.Count} carte nella tua collezione hanno avuto un aumento significativo di prezzo negli ultimi giorni.",
                null,
                "PriceSpike");

            return spikes.Count;
        }

        public async Task<decimal> GetUserSpikeThresholdAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            return user?.PriceSpikeThreshold ?? 10m;
        }

        public async Task UpdateUserSpikeThresholdAsync(int userId, decimal threshold)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return;

            user.PriceSpikeThreshold = Math.Clamp(threshold, 1, 100);
            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();
        }
    }
}
