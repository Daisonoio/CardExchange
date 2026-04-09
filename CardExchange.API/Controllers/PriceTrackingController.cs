using CardExchange.API.Authorization;
using CardExchange.API.DTOs.Responses;
using CardExchange.API.Services;
using CardExchange.Core.Entities;
using CardExchange.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardExchange.API.Controllers
{
    /// <summary>
    /// Tracking valore portfolio, storico prezzi, alert e analisi scambi
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PriceTrackingController : ControllerBase
    {
        private readonly IPriceTrackingService _priceTrackingService;
        private readonly ICardInfoRepository _cardInfoRepository;
        private readonly ILogger<PriceTrackingController> _logger;

        public PriceTrackingController(
            IPriceTrackingService priceTrackingService,
            ICardInfoRepository cardInfoRepository,
            ILogger<PriceTrackingController> logger)
        {
            _priceTrackingService = priceTrackingService;
            _cardInfoRepository = cardInfoRepository;
            _logger = logger;
        }

        /// <summary>
        /// Ottiene il riepilogo del portfolio dell'utente (valore totale, variazione 24h, carta più preziosa)
        /// </summary>
        [HttpGet("portfolio")]
        [RequirePremium]
        public async Task<ActionResult<PortfolioSummaryDto>> GetPortfolioSummary()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0) return Unauthorized();

                var summary = await _priceTrackingService.GetPortfolioSummaryAsync(userId);

                return Ok(new PortfolioSummaryDto
                {
                    TotalValueEur = summary.TotalValueEur,
                    TotalValueUsd = summary.TotalValueUsd,
                    ChangeEur24h = summary.ChangeEur24h,
                    ChangeUsd24h = summary.ChangeUsd24h,
                    ChangePercentage24h = summary.ChangePercentage24h,
                    TotalCards = summary.TotalCards,
                    UniqueCards = summary.UniqueCards,
                    MostValuableCard = summary.MostValuableCard != null ? new CardValueDto
                    {
                        CardInfoId = summary.MostValuableCard.CardInfoId,
                        Name = summary.MostValuableCard.Name,
                        SetName = summary.MostValuableCard.SetName,
                        PriceEur = summary.MostValuableCard.PriceEur,
                        PriceUsd = summary.MostValuableCard.PriceUsd,
                        ImageSmall = summary.MostValuableCard.ImageSmall
                    } : null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il calcolo del portfolio");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Ottiene le carte con la maggiore variazione di prezzo nel portfolio
        /// </summary>
        [HttpGet("portfolio/movers")]
        [RequirePremium]
        public async Task<ActionResult<IEnumerable<MoverDto>>> GetPortfolioMovers([FromQuery] int limit = 10)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0) return Unauthorized();

                limit = Math.Clamp(limit, 1, 50);
                var movers = await _priceTrackingService.GetPortfolioMoversAsync(userId, limit);

                var dtos = movers.Select(m => new MoverDto
                {
                    CardInfoId = m.CardInfoId,
                    Name = m.Name,
                    SetName = m.SetName,
                    CurrentPrice = m.CurrentPrice,
                    PreviousPrice = m.PreviousPrice,
                    ChangeAmount = m.ChangeAmount,
                    ChangePercentage = m.ChangePercentage,
                    ImageSmall = m.ImageSmall
                });

                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero dei movers");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Salva uno snapshot dei prezzi attuali per il portfolio dell'utente
        /// </summary>
        [HttpPost("portfolio/snapshot")]
        [RequirePremium]
        public async Task<ActionResult> TakePortfolioSnapshot()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0) return Unauthorized();

                await _priceTrackingService.SnapshotPricesForUserAsync(userId);
                return Ok(new { message = "Snapshot dei prezzi salvato con successo" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante lo snapshot dei prezzi");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Ottiene lo storico prezzi di una carta (default: 90 giorni)
        /// </summary>
        [HttpGet("history/{cardInfoId}")]
        [RequirePremium]
        public async Task<ActionResult<PriceHistoryDto>> GetPriceHistory(int cardInfoId, [FromQuery] int days = 90)
        {
            try
            {
                days = Math.Clamp(days, 7, 365);

                var cardInfo = await _cardInfoRepository.GetByIdAsync(cardInfoId);
                if (cardInfo == null)
                    return NotFound(new { message = "Carta non trovata" });

                var history = await _priceTrackingService.GetPriceHistoryAsync(cardInfoId, days);

                return Ok(new PriceHistoryDto
                {
                    CardInfoId = cardInfoId,
                    CardName = cardInfo.Name,
                    SetName = cardInfo.CardSet?.Name,
                    DataPoints = history.Select(h => new PricePointDto
                    {
                        Date = h.SnapshotDate,
                        PriceUsd = h.PriceUsd,
                        PriceUsdFoil = h.PriceUsdFoil,
                        PriceEur = h.PriceEur,
                        PriceEurFoil = h.PriceEurFoil
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero dello storico prezzi per {CardInfoId}", cardInfoId);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Ottiene gli alert di prezzo dell'utente
        /// </summary>
        [HttpGet("alerts")]
        [RequirePremium]
        public async Task<ActionResult<IEnumerable<PriceAlertDto>>> GetMyAlerts()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0) return Unauthorized();

                var alerts = await _priceTrackingService.GetUserAlertsAsync(userId);

                var dtos = new List<PriceAlertDto>();
                foreach (var alert in alerts)
                {
                    var cardInfo = await _cardInfoRepository.GetByIdAsync(alert.CardInfoId);
                    var currentPrice = alert.Currency == AlertCurrency.Eur
                        ? cardInfo?.PriceEur
                        : cardInfo?.PriceUsd;

                    dtos.Add(new PriceAlertDto
                    {
                        Id = alert.Id,
                        CardInfoId = alert.CardInfoId,
                        CardName = cardInfo?.Name ?? "N/A",
                        SetName = cardInfo?.CardSet?.Name,
                        TargetPrice = alert.TargetPrice,
                        Direction = alert.Direction.ToString(),
                        Currency = alert.Currency.ToString(),
                        IsActive = alert.IsActive,
                        IsTriggered = alert.IsTriggered,
                        TriggeredAt = alert.TriggeredAt,
                        Notes = alert.Notes,
                        CurrentPrice = currentPrice,
                        ImageSmall = cardInfo?.ImageSmall
                    });
                }

                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero degli alert");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Crea un nuovo alert di prezzo
        /// </summary>
        [HttpPost("alerts")]
        [RequirePremium]
        public async Task<ActionResult<PriceAlertDto>> CreateAlert([FromBody] CreatePriceAlertRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0) return Unauthorized();

                var cardInfo = await _cardInfoRepository.GetByIdAsync(request.CardInfoId);
                if (cardInfo == null)
                    return NotFound(new { message = "Carta non trovata" });

                if (request.TargetPrice <= 0)
                    return BadRequest(new { message = "Il prezzo target deve essere maggiore di zero" });

                var alert = new PriceAlert
                {
                    UserId = userId,
                    CardInfoId = request.CardInfoId,
                    TargetPrice = request.TargetPrice,
                    Direction = (AlertDirection)request.Direction,
                    Currency = (AlertCurrency)request.Currency,
                    Notes = request.Notes
                };

                await _priceTrackingService.CreateAlertAsync(alert);

                var currentPrice = alert.Currency == AlertCurrency.Eur ? cardInfo.PriceEur : cardInfo.PriceUsd;

                return CreatedAtAction(nameof(GetMyAlerts), new PriceAlertDto
                {
                    Id = alert.Id,
                    CardInfoId = alert.CardInfoId,
                    CardName = cardInfo.Name,
                    SetName = cardInfo.CardSet?.Name,
                    TargetPrice = alert.TargetPrice,
                    Direction = alert.Direction.ToString(),
                    Currency = alert.Currency.ToString(),
                    IsActive = true,
                    IsTriggered = false,
                    Notes = alert.Notes,
                    CurrentPrice = currentPrice,
                    ImageSmall = cardInfo.ImageSmall
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la creazione dell'alert");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Elimina un alert di prezzo
        /// </summary>
        [HttpDelete("alerts/{id}")]
        [RequirePremium]
        public async Task<ActionResult> DeleteAlert(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0) return Unauthorized();

                var result = await _priceTrackingService.DeleteAlertAsync(id, userId);
                if (!result)
                    return NotFound(new { message = "Alert non trovato" });

                return Ok(new { message = "Alert eliminato con successo" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'eliminazione dell'alert {AlertId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Controlla e scatena gli alert dell'utente basandosi sui prezzi correnti
        /// </summary>
        [HttpPost("alerts/check")]
        [RequirePremium]
        public async Task<ActionResult> CheckAlerts()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0) return Unauthorized();

                await _priceTrackingService.CheckAndTriggerAlertsAsync(userId);
                return Ok(new { message = "Controllo alert completato" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il controllo degli alert");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Analizza l'equità di uno scambio basandosi sui prezzi di mercato
        /// </summary>
        [HttpPost("analyze-trade")]
        public async Task<ActionResult<TradeAnalysisDto>> AnalyzeTrade([FromBody] TradeAnalyzeRequest request)
        {
            try
            {
                if (!request.OfferedCardIds.Any() && !request.RequestedCardIds.Any())
                    return BadRequest(new { message = "Devi specificare almeno una carta offerta o richiesta" });

                var analysis = await _priceTrackingService.AnalyzeTradeAsync(
                    request.OfferedCardIds, request.RequestedCardIds);

                var verdictDesc = analysis.Verdict switch
                {
                    "fair" => "Lo scambio è equo (differenza entro il 10%)",
                    "in_your_favor" => "Lo scambio è a tuo favore",
                    "against_you" => "Lo scambio è a tuo sfavore",
                    _ => "Non determinabile"
                };

                return Ok(new TradeAnalysisDto
                {
                    OfferedValueEur = analysis.OfferedValueEur,
                    OfferedValueUsd = analysis.OfferedValueUsd,
                    RequestedValueEur = analysis.RequestedValueEur,
                    RequestedValueUsd = analysis.RequestedValueUsd,
                    DifferenceEur = analysis.DifferenceEur,
                    DifferenceUsd = analysis.DifferenceUsd,
                    Verdict = analysis.Verdict,
                    VerdictDescription = verdictDesc,
                    OfferedCards = analysis.OfferedCards.Select(c => new TradeCardValueDto
                    {
                        CardId = c.CardId,
                        Name = c.Name,
                        PriceEur = c.PriceEur,
                        PriceUsd = c.PriceUsd,
                        ImageSmall = c.ImageSmall
                    }),
                    RequestedCards = analysis.RequestedCards.Select(c => new TradeCardValueDto
                    {
                        CardId = c.CardId,
                        Name = c.Name,
                        PriceEur = c.PriceEur,
                        PriceUsd = c.PriceUsd,
                        ImageSmall = c.ImageSmall
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'analisi dello scambio");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Ottiene le carte con spike di prezzo nella collezione dell'utente
        /// </summary>
        [HttpGet("spikes")]
        public async Task<ActionResult<IEnumerable<PriceSpikeDto>>> GetPriceSpikes([FromQuery] int? gameId = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0) return Unauthorized();

                var spikes = await _priceTrackingService.DetectPriceSpikesAsync(userId, gameId);

                var dtos = spikes.Select(s => new PriceSpikeDto
                {
                    CardId = s.CardId,
                    CardInfoId = s.CardInfoId,
                    Name = s.Name,
                    SetName = s.SetName,
                    ImageSmall = s.ImageSmall,
                    CurrentPriceEur = s.CurrentPriceEur,
                    OldPriceEur = s.OldPriceEur,
                    ChangePercentage = s.ChangePercentage,
                    ChangeAmount = s.ChangeAmount,
                    Last5Days = s.Last5Days.Select(d => new PriceDayPointDto
                    {
                        Date = d.Date,
                        PriceEur = d.PriceEur
                    }).ToList()
                });

                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il rilevamento spike prezzi");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Controlla spike e invia notifica se ci sono carte in salita
        /// </summary>
        [HttpPost("spikes/check")]
        public async Task<ActionResult> CheckSpikes()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0) return Unauthorized();

                var count = await _priceTrackingService.CheckAndNotifySpikesAsync(userId);
                return Ok(new { spikeCount = count, message = count > 0
                    ? $"{count} carte con spike rilevate"
                    : "Nessuno spike rilevato" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il controllo spike");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Ottiene le impostazioni soglia spike dell'utente
        /// </summary>
        [HttpGet("spike-settings")]
        public async Task<ActionResult<SpikeSettingsDto>> GetSpikeSettings()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0) return Unauthorized();

                var threshold = await _priceTrackingService.GetUserSpikeThresholdAsync(userId);
                return Ok(new SpikeSettingsDto { ThresholdPercentage = threshold });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero delle impostazioni spike");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Aggiorna la soglia % spike dell'utente
        /// </summary>
        [HttpPut("spike-settings")]
        public async Task<ActionResult> UpdateSpikeSettings([FromBody] UpdateSpikeSettingsRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0) return Unauthorized();

                if (request.ThresholdPercentage < 1 || request.ThresholdPercentage > 100)
                    return BadRequest(new { message = "La soglia deve essere tra 1% e 100%" });

                await _priceTrackingService.UpdateUserSpikeThresholdAsync(userId, request.ThresholdPercentage);
                return Ok(new { message = "Impostazioni aggiornate" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'aggiornamento delle impostazioni spike");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }
    }
}
