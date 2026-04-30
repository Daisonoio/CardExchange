using CardExchange.API.Authorization;
using CardExchange.API.DTOs.Requests;
using CardExchange.API.DTOs.Responses;
using CardExchange.API.Services;
using CardExchange.Core.Constants;
using CardExchange.Core.Entities;
using CardExchange.Core.Interfaces;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CardExchange.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TradeOffersController : ControllerBase
    {
        private readonly ITradeOfferRepository _tradeOfferRepository;
        private readonly ICardRepository _cardRepository;
        private readonly IUserRepository _userRepository;
        private readonly ISubscriptionService _subscriptionService;
        private readonly INotificationService _notificationService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TradeOffersController> _logger;

        public TradeOffersController(
            ITradeOfferRepository tradeOfferRepository,
            ICardRepository cardRepository,
            IUserRepository userRepository,
            ISubscriptionService subscriptionService,
            INotificationService notificationService,
            ApplicationDbContext context,
            ILogger<TradeOffersController> logger)
        {
            _tradeOfferRepository = tradeOfferRepository;
            _cardRepository = cardRepository;
            _userRepository = userRepository;
            _subscriptionService = subscriptionService;
            _notificationService = notificationService;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Ottiene le offerte di scambio dell'utente corrente (paginato)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMyOffers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] TradeOfferStatus? status = null)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var (offers, totalCount) = await _tradeOfferRepository.GetUserOffersPagedAsync(userId, page, pageSize, status);

            return Ok(new PagedResult<TradeOfferDto>
            {
                Items = offers.Select(MapToDto),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }

        /// <summary>
        /// Ottiene il dettaglio di un'offerta di scambio
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetOffer(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var offer = await _tradeOfferRepository.GetWithDetailsAsync(id);
            if (offer == null)
                return NotFound(new { message = ErrorMessages.TradeOfferNotFound });

            if (offer.SenderId != userId && offer.ReceiverId != userId)
                return Forbid();

            return Ok(MapToDto(offer));
        }

        /// <summary>
        /// Crea una nuova offerta di scambio
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateOffer([FromBody] CreateTradeOfferRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            if (request.ReceiverId == userId)
                return BadRequest(new { message = ErrorMessages.CannotTradeWithSelf });

            // Verifica limiti piano
            var activeCount = await _tradeOfferRepository.GetActiveOfferCountAsync(userId);
            if (!await _subscriptionService.CheckLimitAsync(userId, "trades", activeCount))
            {
                var plan = await _subscriptionService.GetUserActivePlanAsync(userId);
                return StatusCode(403, new
                {
                    message = $"Hai raggiunto il limite di {plan?.MaxActiveTradeOffers ?? FreeTierLimits.MaxActiveTradeOffers} offerte attive",
                    upgradeUrl = "/api/subscriptions/plans"
                });
            }

            // Verifica che il destinatario esista
            var receiver = await _userRepository.GetByIdAsync(request.ReceiverId);
            if (receiver == null)
                return NotFound(new { message = "Destinatario non trovato" });

            // Verifica che le carte offerte appartengano al mittente e siano disponibili
            foreach (var item in request.OfferedCards)
            {
                var card = await _cardRepository.GetByIdAsync(item.CardId);
                if (card == null)
                    return BadRequest(new { message = $"Carta offerta con ID {item.CardId} non trovata" });
                if (card.UserId != userId)
                    return BadRequest(new { message = $"La carta {item.CardId} non ti appartiene" });
                if (!card.IsAvailableForTrade)
                    return BadRequest(new { message = $"La carta {item.CardId} non è disponibile per lo scambio" });
            }

            // Verifica che le carte richieste appartengano al destinatario
            foreach (var item in request.RequestedCards)
            {
                var card = await _cardRepository.GetByIdAsync(item.CardId);
                if (card == null)
                    return BadRequest(new { message = $"Carta richiesta con ID {item.CardId} non trovata" });
                if (card.UserId != request.ReceiverId)
                    return BadRequest(new { message = $"La carta {item.CardId} non appartiene al destinatario" });
                if (!card.IsAvailableForTrade)
                    return BadRequest(new { message = $"La carta {item.CardId} non è disponibile per lo scambio" });
            }

            var offer = new TradeOffer
            {
                SenderId = userId,
                ReceiverId = request.ReceiverId,
                Message = request.Message,
                Status = TradeOfferStatus.Pending,
                ExpiresAt = DateTime.UtcNow.AddDays(FreeTierLimits.TradeOfferExpirationDays)
            };

            // Aggiungi le carte offerte
            foreach (var item in request.OfferedCards)
            {
                offer.Items.Add(new TradeOfferItem
                {
                    CardId = item.CardId,
                    Side = TradeOfferItemSide.Offered,
                    Quantity = item.Quantity
                });
            }

            // Aggiungi le carte richieste
            foreach (var item in request.RequestedCards)
            {
                offer.Items.Add(new TradeOfferItem
                {
                    CardId = item.CardId,
                    Side = TradeOfferItemSide.Requested,
                    Quantity = item.Quantity
                });
            }

            await _tradeOfferRepository.AddAsync(offer);
            await _tradeOfferRepository.SaveChangesAsync();

            // Notifica al destinatario
            var sender = await _userRepository.GetByIdAsync(userId);
            await _notificationService.SendTradeOfferNotificationAsync(
                request.ReceiverId, NotificationType.TradeOfferReceived, offer.Id, sender!.Username);

            _logger.LogInformation("Offerta di scambio creata: {OfferId} da {SenderId} a {ReceiverId}", offer.Id, userId, request.ReceiverId);

            var createdOffer = await _tradeOfferRepository.GetWithDetailsAsync(offer.Id);
            return CreatedAtAction(nameof(GetOffer), new { id = offer.Id }, MapToDto(createdOffer!));
        }

        /// <summary>
        /// Accetta un'offerta di scambio
        /// </summary>
        [HttpPost("{id}/accept")]
        public async Task<IActionResult> AcceptOffer(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var offer = await _tradeOfferRepository.GetWithDetailsAsync(id);
            if (offer == null)
                return NotFound(new { message = ErrorMessages.TradeOfferNotFound });

            if (offer.ReceiverId != userId)
                return Forbid();

            if (offer.Status != TradeOfferStatus.Pending)
                return BadRequest(new { message = "L'offerta non è in stato 'In attesa'" });

            if (offer.ExpiresAt.HasValue && offer.ExpiresAt < DateTime.UtcNow)
                return BadRequest(new { message = ErrorMessages.TradeOfferExpired });

            var availabilityCheck = await ValidateOfferCardsStillAvailableAsync(offer);
            if (!availabilityCheck.IsValid)
                return BadRequest(new { message = availabilityCheck.ErrorMessage });

            offer.Status = TradeOfferStatus.Accepted;
            offer.ResponseDate = DateTime.UtcNow;

            _tradeOfferRepository.Update(offer);
            await _tradeOfferRepository.SaveChangesAsync();

            await _notificationService.SendTradeOfferNotificationAsync(
                offer.SenderId, NotificationType.TradeOfferAccepted, offer.Id, offer.Receiver.Username);

            _logger.LogInformation("Offerta {OfferId} accettata da {UserId}", id, userId);

            return Ok(MapToDto(offer));
        }

        /// <summary>
        /// Rifiuta un'offerta di scambio
        /// </summary>
        [HttpPost("{id}/reject")]
        public async Task<IActionResult> RejectOffer(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var offer = await _tradeOfferRepository.GetWithDetailsAsync(id);
            if (offer == null)
                return NotFound(new { message = ErrorMessages.TradeOfferNotFound });

            if (offer.ReceiverId != userId)
                return Forbid();

            if (offer.Status != TradeOfferStatus.Pending)
                return BadRequest(new { message = "L'offerta non è in stato 'In attesa'" });

            offer.Status = TradeOfferStatus.Rejected;
            offer.ResponseDate = DateTime.UtcNow;

            _tradeOfferRepository.Update(offer);
            await _tradeOfferRepository.SaveChangesAsync();

            await _notificationService.SendTradeOfferNotificationAsync(
                offer.SenderId, NotificationType.TradeOfferRejected, offer.Id, offer.Receiver.Username);

            _logger.LogInformation("Offerta {OfferId} rifiutata da {UserId}", id, userId);

            return Ok(MapToDto(offer));
        }

        /// <summary>
        /// Annulla un'offerta di scambio (solo il mittente)
        /// </summary>
        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> CancelOffer(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var offer = await _tradeOfferRepository.GetWithDetailsAsync(id);
            if (offer == null)
                return NotFound(new { message = ErrorMessages.TradeOfferNotFound });

            if (offer.SenderId != userId)
                return Forbid();

            if (offer.Status != TradeOfferStatus.Pending)
                return BadRequest(new { message = "Solo le offerte in attesa possono essere annullate" });

            offer.Status = TradeOfferStatus.Cancelled;
            offer.ResponseDate = DateTime.UtcNow;

            _tradeOfferRepository.Update(offer);
            await _tradeOfferRepository.SaveChangesAsync();

            _logger.LogInformation("Offerta {OfferId} annullata da {UserId}", id, userId);

            return Ok(MapToDto(offer));
        }

        /// <summary>
        /// Crea una contro-offerta
        /// </summary>
        [HttpPost("{id}/counter")]
        public async Task<IActionResult> CounterOffer(int id, [FromBody] CounterOfferRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var originalOffer = await _tradeOfferRepository.GetWithDetailsAsync(id);
            if (originalOffer == null)
                return NotFound(new { message = ErrorMessages.TradeOfferNotFound });

            if (originalOffer.ReceiverId != userId)
                return Forbid();

            if (originalOffer.Status != TradeOfferStatus.Pending)
                return BadRequest(new { message = "Puoi contro-offrire solo su offerte in attesa" });

            // Segna l'offerta originale come counter-offer
            originalOffer.Status = TradeOfferStatus.CounterOffer;
            originalOffer.ResponseDate = DateTime.UtcNow;
            _tradeOfferRepository.Update(originalOffer);

            // Crea la nuova offerta invertendo i ruoli
            var counterOffer = new TradeOffer
            {
                SenderId = userId,
                ReceiverId = originalOffer.SenderId,
                Message = request.Message,
                Status = TradeOfferStatus.Pending,
                ParentOfferId = id,
                ExpiresAt = DateTime.UtcNow.AddDays(FreeTierLimits.TradeOfferExpirationDays)
            };

            foreach (var item in request.OfferedCards)
            {
                var card = await _cardRepository.GetByIdAsync(item.CardId);
                if (card == null || card.UserId != userId || !card.IsAvailableForTrade)
                    return BadRequest(new { message = $"Carta {item.CardId} non valida per l'offerta" });

                counterOffer.Items.Add(new TradeOfferItem
                {
                    CardId = item.CardId,
                    Side = TradeOfferItemSide.Offered,
                    Quantity = item.Quantity
                });
            }

            foreach (var item in request.RequestedCards)
            {
                var card = await _cardRepository.GetByIdAsync(item.CardId);
                if (card == null || card.UserId != originalOffer.SenderId || !card.IsAvailableForTrade)
                    return BadRequest(new { message = $"Carta richiesta {item.CardId} non valida" });

                counterOffer.Items.Add(new TradeOfferItem
                {
                    CardId = item.CardId,
                    Side = TradeOfferItemSide.Requested,
                    Quantity = item.Quantity
                });
            }

            await _tradeOfferRepository.AddAsync(counterOffer);
            await _tradeOfferRepository.SaveChangesAsync();

            var currentUser = await _userRepository.GetByIdAsync(userId);
            await _notificationService.SendTradeOfferNotificationAsync(
                originalOffer.SenderId, NotificationType.TradeOfferReceived, counterOffer.Id, currentUser!.Username);

            _logger.LogInformation("Contro-offerta {CounterOfferId} creata per offerta {OriginalOfferId}", counterOffer.Id, id);

            var created = await _tradeOfferRepository.GetWithDetailsAsync(counterOffer.Id);
            return CreatedAtAction(nameof(GetOffer), new { id = counterOffer.Id }, MapToDto(created!));
        }

        /// <summary>
        /// Completa uno scambio (entrambe le parti devono confermare)
        /// </summary>
        [HttpPost("{id}/complete")]
        public async Task<IActionResult> CompleteOffer(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var offer = await _tradeOfferRepository.GetWithDetailsAsync(id);
            if (offer == null)
                return NotFound(new { message = ErrorMessages.TradeOfferNotFound });

            if (offer.SenderId != userId && offer.ReceiverId != userId)
                return Forbid();

            if (offer.Status != TradeOfferStatus.Accepted)
                return BadRequest(new { message = "Solo le offerte accettate possono essere completate" });

            var availabilityCheck = await ValidateOfferCardsStillAvailableAsync(offer);
            if (!availabilityCheck.IsValid)
                return BadRequest(new { message = availabilityCheck.ErrorMessage });

            User? sender;
            User? receiver;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var transferResult = await ExecuteTradeTransferAsync(offer);
                if (!transferResult.IsSuccess)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { message = transferResult.ErrorMessage });
                }

                offer.Status = TradeOfferStatus.Completed;
                offer.CompletedDate = DateTime.UtcNow;

                sender = await _userRepository.GetByIdAsync(offer.SenderId);
                receiver = await _userRepository.GetByIdAsync(offer.ReceiverId);
                if (sender != null) sender.TotalTradesCompleted++;
                if (receiver != null) receiver.TotalTradesCompleted++;

                _tradeOfferRepository.Update(offer);
                await _tradeOfferRepository.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Errore durante il completamento dello scambio {OfferId}", id);
                return StatusCode(500, new { message = "Errore durante il completamento dello scambio" });
            }

            var otherUserId = userId == offer.SenderId ? offer.ReceiverId : offer.SenderId;
            var currentUser = userId == offer.SenderId ? sender : receiver;
            await _notificationService.SendTradeOfferNotificationAsync(
                otherUserId, NotificationType.TradeCompleted, offer.Id, currentUser!.Username);

            _logger.LogInformation("Scambio {OfferId} completato", id);

            return Ok(MapToDto(offer));
        }

        /// <summary>
        /// Aggiunge una recensione a uno scambio completato
        /// </summary>
        [HttpPost("{id}/review")]
        public async Task<IActionResult> AddReview(int id, [FromBody] CreateTradeReviewRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var offer = await _tradeOfferRepository.GetWithDetailsAsync(id);
            if (offer == null)
                return NotFound(new { message = ErrorMessages.TradeOfferNotFound });

            if (offer.SenderId != userId && offer.ReceiverId != userId)
                return Forbid();

            if (offer.Status != TradeOfferStatus.Completed)
                return BadRequest(new { message = ErrorMessages.TradeNotCompleted });

            // Verifica che non abbia già recensito
            if (offer.Reviews.Any(r => r.ReviewerId == userId))
                return BadRequest(new { message = ErrorMessages.AlreadyReviewed });

            var reviewedUserId = userId == offer.SenderId ? offer.ReceiverId : offer.SenderId;

            var review = new TradeReview
            {
                TradeOfferId = id,
                ReviewerId = userId,
                ReviewedUserId = reviewedUserId,
                Rating = request.Rating,
                Comment = request.Comment,
                CardAsDescribed = request.CardAsDescribed,
                TimelyShipping = request.TimelyShipping,
                GoodCommunication = request.GoodCommunication
            };

            User? reviewedUser;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                offer.Reviews.Add(review);

                reviewedUser = await _userRepository.GetByIdAsync(reviewedUserId);
                if (reviewedUser != null)
                {
                    reviewedUser.TotalReviewsReceived++;
                    var totalRating = reviewedUser.ReputationScore * (reviewedUser.TotalReviewsReceived - 1) + request.Rating;
                    reviewedUser.ReputationScore = totalRating / reviewedUser.TotalReviewsReceived;
                }

                await _tradeOfferRepository.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Errore durante l'aggiunta della recensione per lo scambio {OfferId}", id);
                return StatusCode(500, new { message = "Errore durante il salvataggio della recensione" });
            }

            var reviewer = await _userRepository.GetByIdAsync(userId);
            await _notificationService.SendAsync(
                reviewedUserId,
                NotificationType.NewReview,
                "Nuova recensione ricevuta",
                $"{reviewer!.Username} ti ha lasciato una recensione ({request.Rating}/5)",
                id,
                "TradeOffer");

            _logger.LogInformation("Recensione aggiunta per scambio {OfferId} da {UserId}", id, userId);

            return Ok(new TradeReviewDto
            {
                Id = review.Id,
                TradeOfferId = id,
                ReviewerId = userId,
                ReviewerUsername = reviewer.Username,
                ReviewedUserId = reviewedUserId,
                ReviewedUserUsername = reviewedUser?.Username ?? string.Empty,
                Rating = review.Rating,
                Comment = review.Comment,
                CardAsDescribed = review.CardAsDescribed,
                TimelyShipping = review.TimelyShipping,
                GoodCommunication = review.GoodCommunication,
                CreatedAt = review.CreatedAt
            });
        }

        /// <summary>
        /// Ottiene le recensioni di un utente
        /// </summary>
        [HttpGet("reviews/user/{userId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetUserReviews(int userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return NotFound(new { message = ErrorMessages.UserNotFound });

            var offers = await _tradeOfferRepository.GetUserOffersAsync(userId, TradeOfferStatus.Completed);
            var reviews = offers
                .SelectMany(o => o.Reviews)
                .Where(r => r.ReviewedUserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            var totalCount = reviews.Count;
            var pagedReviews = reviews.Skip((page - 1) * pageSize).Take(pageSize);

            return Ok(new
            {
                userId,
                username = user.Username,
                reputationScore = user.ReputationScore,
                totalReviews = totalCount,
                reviews = new PagedResult<TradeReviewDto>
                {
                    Items = pagedReviews.Select(r => new TradeReviewDto
                    {
                        Id = r.Id,
                        TradeOfferId = r.TradeOfferId,
                        ReviewerId = r.ReviewerId,
                        ReviewerUsername = r.Reviewer?.Username ?? string.Empty,
                        ReviewedUserId = r.ReviewedUserId,
                        ReviewedUserUsername = user.Username,
                        Rating = r.Rating,
                        Comment = r.Comment,
                        CardAsDescribed = r.CardAsDescribed,
                        TimelyShipping = r.TimelyShipping,
                        GoodCommunication = r.GoodCommunication,
                        CreatedAt = r.CreatedAt
                    }),
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize
                }
            });
        }

        private static TradeOfferDto MapToDto(TradeOffer offer)
        {
            return new TradeOfferDto
            {
                Id = offer.Id,
                SenderId = offer.SenderId,
                SenderUsername = offer.Sender?.Username ?? string.Empty,
                ReceiverId = offer.ReceiverId,
                ReceiverUsername = offer.Receiver?.Username ?? string.Empty,
                Status = offer.Status.ToString(),
                Message = offer.Message,
                CreatedAt = offer.CreatedAt,
                ResponseDate = offer.ResponseDate,
                CompletedDate = offer.CompletedDate,
                ExpiresAt = offer.ExpiresAt,
                ParentOfferId = offer.ParentOfferId,
                OfferedCards = offer.Items
                    .Where(i => i.Side == TradeOfferItemSide.Offered)
                    .Select(MapItemToDto).ToList(),
                RequestedCards = offer.Items
                    .Where(i => i.Side == TradeOfferItemSide.Requested)
                    .Select(MapItemToDto).ToList()
            };
        }

        private static TradeOfferItemDto MapItemToDto(TradeOfferItem item)
        {
            return new TradeOfferItemDto
            {
                Id = item.Id,
                CardId = item.CardId,
                CardName = item.Card?.CardInfo?.Name ?? string.Empty,
                CardSetName = item.Card?.CardInfo?.CardSet?.Name ?? string.Empty,
                Condition = item.Card?.Condition.ToString() ?? string.Empty,
                Quantity = item.Quantity,
                OwnerUsername = item.Card?.User?.Username ?? string.Empty
            };
        }

        private async Task<(bool IsValid, string ErrorMessage)> ValidateOfferCardsStillAvailableAsync(TradeOffer offer)
        {
            foreach (var item in offer.Items)
            {
                var card = await _cardRepository.GetByIdAsync(item.CardId);
                if (card == null || card.IsDeleted)
                    return (false, $"La carta {item.CardId} non è più disponibile");

                var expectedOwnerId = item.Side == TradeOfferItemSide.Offered ? offer.SenderId : offer.ReceiverId;
                if (card.UserId != expectedOwnerId)
                    return (false, $"La carta {item.CardId} non appartiene più al proprietario iniziale");

                if (!card.IsAvailableForTrade)
                    return (false, $"La carta {item.CardId} non è più disponibile per lo scambio");

                if (card.Quantity < item.Quantity)
                    return (false, $"Quantità insufficiente per la carta {item.CardId}");
            }

            return (true, string.Empty);
        }

        private async Task<(bool IsSuccess, string ErrorMessage)> ExecuteTradeTransferAsync(TradeOffer offer)
        {
            foreach (var item in offer.Items)
            {
                var sourceOwnerId = item.Side == TradeOfferItemSide.Offered ? offer.SenderId : offer.ReceiverId;
                var destinationOwnerId = item.Side == TradeOfferItemSide.Offered ? offer.ReceiverId : offer.SenderId;

                var sourceCard = await _cardRepository.GetByIdAsync(item.CardId);
                if (sourceCard == null || sourceCard.IsDeleted)
                    return (false, $"Carta {item.CardId} non trovata durante il trasferimento");

                if (sourceCard.UserId != sourceOwnerId || sourceCard.Quantity < item.Quantity)
                    return (false, $"Trasferimento non valido per carta {item.CardId}");

                sourceCard.Quantity -= item.Quantity;
                if (sourceCard.Quantity <= 0)
                {
                    _cardRepository.Delete(sourceCard);
                }
                else
                {
                    _cardRepository.Update(sourceCard);
                }

                var destinationCard = (await _cardRepository.GetUserCardsAsync(destinationOwnerId))
                    .FirstOrDefault(c =>
                        !c.IsDeleted &&
                        c.CardInfoId == sourceCard.CardInfoId &&
                        c.Condition == sourceCard.Condition &&
                        (c.Notes ?? string.Empty) == (sourceCard.Notes ?? string.Empty));

                if (destinationCard != null)
                {
                    destinationCard.Quantity += item.Quantity;
                    destinationCard.IsAvailableForTrade = true;
                    _cardRepository.Update(destinationCard);
                }
                else
                {
                    await _cardRepository.AddAsync(new Card
                    {
                        UserId = destinationOwnerId,
                        CardInfoId = sourceCard.CardInfoId,
                        Condition = sourceCard.Condition,
                        Quantity = item.Quantity,
                        Notes = sourceCard.Notes,
                        IsAvailableForTrade = true,
                        EstimatedValue = sourceCard.EstimatedValue,
                    });
                }
            }

            return (true, string.Empty);
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
        }
    }
}
