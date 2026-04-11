using CardExchange.API.Authorization;
using CardExchange.API.DTOs.Requests;
using CardExchange.API.DTOs.Responses;
using CardExchange.API.Services;
using CardExchange.Core.Entities;
using CardExchange.Core.Interfaces;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CardsController : ControllerBase
    {
        private readonly ICardRepository _cardRepository;
        private readonly IUserRepository _userRepository;
        private readonly ICardInfoRepository _cardInfoRepository;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IScryfallService _scryfallService;
        private readonly INotificationService _notificationService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CardsController> _logger;

        public CardsController(
            ICardRepository cardRepository,
            IUserRepository userRepository,
            ICardInfoRepository cardInfoRepository,
            ISubscriptionService subscriptionService,
            IScryfallService scryfallService,
            INotificationService notificationService,
            ApplicationDbContext context,
            ILogger<CardsController> logger)
        {
            _cardRepository = cardRepository;
            _userRepository = userRepository;
            _cardInfoRepository = cardInfoRepository;
            _subscriptionService = subscriptionService;
            _scryfallService = scryfallService;
            _notificationService = notificationService;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Ottiene tutte le carte disponibili per lo scambio
        /// </summary>
        [HttpGet]
        [RequirePermission("CARDS.READ.ALL")]
        public async Task<ActionResult<IEnumerable<CardDto>>> GetAllAvailableCards([FromQuery] int? gameId = null)
        {
            try
            {
                var cards = gameId.HasValue
                    ? await _cardRepository.GetAvailableCardsAsync(gameId.Value)
                    : await _cardRepository.GetAvailableCardsAsync();
                var cardDtos = cards.Select(MapToDto);

                return Ok(new
                {
                    count = cardDtos.Count(),
                    cards = cardDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero delle carte disponibili");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Ottiene una carta specifica per ID
        /// </summary>
        [HttpGet("{id}")]
        [RequirePermission("CARDS.READ.ALL")]
        public async Task<ActionResult<CardDetailDto>> GetCardById(int id)
        {
            try
            {
                var card = await _cardRepository.GetCardWithDetailsAsync(id);

                if (card == null)
                {
                    return NotFound(new { message = $"Carta con ID {id} non trovata" });
                }

                return Ok(await MapToDetailDtoAsync(card));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero della carta {CardId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Ottiene tutte le carte di un utente specifico
        /// </summary>
        [HttpGet("user/{userId}")]
        [RequirePermission("CARDS.READ.ALL")]
        public async Task<ActionResult<IEnumerable<CardDto>>> GetUserCards(int userId, [FromQuery] int? gameId = null)
        {
            try
            {
                var userExists = await _userRepository.GetByIdAsync(userId);
                if (userExists == null)
                {
                    return NotFound(new { message = $"Utente con ID {userId} non trovato" });
                }

                var cards = await _cardRepository.GetUserCardsAsync(userId);

                if (gameId.HasValue)
                {
                    cards = cards.Where(c => c.CardInfo?.CardSet?.GameId == gameId.Value);
                }

                var cardDtos = cards.Select(MapToDto);

                return Ok(new
                {
                    userId,
                    username = userExists.Username,
                    count = cardDtos.Count(),
                    cards = cardDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero delle carte dell'utente {UserId}", userId);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Cerca carte per nome o set
        /// </summary>
        [HttpGet("search")]
        [RequirePermission("SEARCH.BASIC")]
        public async Task<ActionResult<IEnumerable<CardDto>>> SearchCards([FromQuery] string searchTerm)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    return BadRequest(new { message = "Il termine di ricerca è obbligatorio" });
                }

                if (searchTerm.Length < 2)
                {
                    return BadRequest(new { message = "Il termine di ricerca deve essere di almeno 2 caratteri" });
                }

                var cards = await _cardRepository.SearchCardsAsync(searchTerm);
                var cardDtos = cards.Select(MapToDto);

                return Ok(new
                {
                    searchTerm,
                    count = cardDtos.Count(),
                    cards = cardDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la ricerca delle carte con termine: {SearchTerm}", searchTerm);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Cerca carte per località
        /// </summary>
        [HttpGet("by-location")]
        [RequirePermission("SEARCH.GEOGRAPHIC")]
        public async Task<ActionResult<IEnumerable<CardDto>>> GetCardsByLocation(
            [FromQuery] string city,
            [FromQuery] string province,
            [FromQuery] string country)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(province) || string.IsNullOrWhiteSpace(country))
                {
                    return BadRequest(new { message = "City, province e country sono obbligatori" });
                }

                var cards = await _cardRepository.GetCardsByLocationAsync(city, province, country);
                var cardDtos = cards.Select(MapToDto);

                return Ok(new
                {
                    location = new { city, province, country },
                    count = cardDtos.Count(),
                    cards = cardDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la ricerca carte per località");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Cerca carte per condizione
        /// </summary>
        [HttpGet("by-condition/{condition}")]
        [RequirePermission("CARDS.READ.ALL")]
        public async Task<ActionResult<IEnumerable<CardDto>>> GetCardsByCondition(int condition)
        {
            try
            {
                if (!Enum.IsDefined(typeof(CardCondition), condition))
                {
                    return BadRequest(new { message = "Condizione non valida. Valori ammessi: 1-8" });
                }

                var cardCondition = (CardCondition)condition;
                var cards = await _cardRepository.GetCardsByConditionAsync(cardCondition);
                var cardDtos = cards.Select(MapToDto);

                return Ok(new
                {
                    condition = cardCondition.ToString(),
                    count = cardDtos.Count(),
                    cards = cardDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la ricerca carte per condizione {Condition}", condition);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Ottiene tutte le carte disponibili per una specifica CardInfo
        /// </summary>
        [HttpGet("by-cardinfo/{cardInfoId}")]
        [RequirePermission("CARDS.READ.ALL")]
        public async Task<ActionResult<IEnumerable<CardDto>>> GetCardsByCardInfo(int cardInfoId)
        {
            try
            {
                var cardInfoExists = await _cardInfoRepository.GetByIdAsync(cardInfoId);
                if (cardInfoExists == null)
                {
                    return NotFound(new { message = $"CardInfo con ID {cardInfoId} non trovata" });
                }

                var cards = await _cardRepository.GetCardsByCardInfoAsync(cardInfoId);
                var cardDtos = cards.Select(MapToDto);

                return Ok(new
                {
                    cardInfoId,
                    cardName = cardInfoExists.Name,
                    availableCount = cardDtos.Count(),
                    cards = cardDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero delle carte per CardInfo {CardInfoId}", cardInfoId);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Aggiunge una carta alla collezione di un utente
        /// </summary>
        [HttpPost("user/{userId}")]
        [RequirePermission("CARDS.CREATE.OWN")]
        public async Task<ActionResult<CardDto>> CreateCard(int userId, [FromBody] CreateCardRequest request)
        {
            try
            {
                // Verifica che l'utente esista
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    return NotFound(new { message = $"Utente con ID {userId} non trovato" });
                }

                // Verifica limiti piano (free tier)
                var userCards = await _cardRepository.GetUserCardsAsync(userId);
                var currentCardCount = userCards.Count();
                if (!await _subscriptionService.CheckLimitAsync(userId, "cards", currentCardCount))
                {
                    var plan = await _subscriptionService.GetUserActivePlanAsync(userId);
                    return StatusCode(403, new
                    {
                        message = $"Hai raggiunto il limite di {plan?.MaxCards ?? 50} carte nel tuo piano",
                        upgradeUrl = "/api/subscriptions/plans"
                    });
                }

                // Verifica che la CardInfo esista
                var cardInfo = await _cardInfoRepository.GetByIdAsync(request.CardInfoId);
                if (cardInfo == null)
                {
                    return NotFound(new { message = $"CardInfo con ID {request.CardInfoId} non trovata" });
                }

                // Validazione e arricchimento da Scryfall (se la carta ha un ScryfallId)
                if (cardInfo.ScryfallId != null && cardInfo.ScryfallUpdatedAt == null)
                {
                    var scryfallCard = await _scryfallService.GetCardByScryfallIdAsync(cardInfo.ScryfallId);
                    if (scryfallCard != null)
                    {
                        _scryfallService.MapScryfallToCardInfo(scryfallCard, cardInfo);
                        await _cardInfoRepository.SaveChangesAsync();
                    }
                }

                var card = new Card
                {
                    UserId = userId,
                    CardInfoId = request.CardInfoId,
                    Condition = (CardCondition)request.Condition,
                    Quantity = request.Quantity,  // ← AGGIUNTO
                    Notes = request.Notes,
                    IsAvailableForTrade = request.IsAvailableForTrade,
                    EstimatedValue = request.EstimatedValue
                };

                await _cardRepository.AddAsync(card);
                await _cardRepository.SaveChangesAsync();

                _logger.LogInformation("Carta aggiunta alla collezione dell'utente {UserId}: {CardId}", userId, card.Id);

                // Ricarica la carta con tutte le relazioni
                var createdCard = await _cardRepository.GetUserCardAsync(userId, card.Id);
                return CreatedAtAction(nameof(GetCardById), new { id = card.Id }, MapToDto(createdCard!));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'aggiunta della carta all'utente {UserId}", userId);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Aggiorna una carta esistente
        /// </summary>
        [HttpPut("{id}")]
        [RequirePermission("CARDS.UPDATE.OWN", "CARDS.DELETE.ANY")]
        public async Task<ActionResult<CardDto>> UpdateCard(int id, [FromBody] UpdateCardRequest request)
        {
            try
            {
                var card = await _cardRepository.GetByIdAsync(id);

                if (card == null)
                {
                    return NotFound(new { message = $"Carta con ID {id} non trovata" });
                }

                // Aggiorna solo i campi forniti
                if (request.Condition.HasValue)
                {
                    if (!Enum.IsDefined(typeof(CardCondition), request.Condition.Value))
                    {
                        return BadRequest(new { message = "Condizione non valida" });
                    }
                    card.Condition = (CardCondition)request.Condition.Value;
                }

                if (request.Quantity.HasValue)  // ← AGGIUNTO
                {
                    if (request.Quantity.Value < 1)
                    {
                        return BadRequest(new { message = "La quantità deve essere almeno 1" });
                    }
                    card.Quantity = request.Quantity.Value;
                }

                if (request.Notes != null)
                    card.Notes = request.Notes;

                if (request.IsAvailableForTrade.HasValue)
                    card.IsAvailableForTrade = request.IsAvailableForTrade.Value;

                var oldPrice = card.EstimatedValue;
                var oldAvailable = card.IsAvailableForTrade;

                if (request.EstimatedValue.HasValue)
                    card.EstimatedValue = request.EstimatedValue.Value;

                _cardRepository.Update(card);
                await _cardRepository.SaveChangesAsync();

                _logger.LogInformation("Carta aggiornata: {CardId}", id);

                // Notify favorite holders of price/availability changes
                try
                {
                    var cardName = card.CardInfo?.Name ?? $"Carta #{id}";
                    if (request.EstimatedValue.HasValue && oldPrice != request.EstimatedValue.Value)
                    {
                        await _notificationService.NotifyFavoriteCardChangedAsync(id, cardName, "price");
                    }
                    if (request.IsAvailableForTrade.HasValue && oldAvailable && !request.IsAvailableForTrade.Value)
                    {
                        await _notificationService.NotifyFavoriteCardChangedAsync(id, cardName, "removed");
                    }
                }
                catch (Exception notifEx)
                {
                    _logger.LogWarning(notifEx, "Errore invio notifica preferiti per carta {CardId}", id);
                }

                // Ricarica con le relazioni
                var updatedCard = (await _cardRepository.FindAsync(c => c.Id == id)).FirstOrDefault();
                return Ok(MapToDto(updatedCard!));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'aggiornamento della carta {CardId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Elimina una carta dalla collezione (soft delete)
        /// </summary>
        [HttpDelete("{id}")]
        [RequirePermission("CARDS.DELETE.OWN", "CARDS.DELETE.ANY")]
        public async Task<IActionResult> DeleteCard(int id)
        {
            try
            {
                var card = await _cardRepository.GetByIdAsync(id);

                if (card == null)
                {
                    return NotFound(new { message = $"Carta con ID {id} non trovata" });
                }

                // Notify favorite holders before deletion
                try
                {
                    var cardName = card.CardInfo?.Name ?? $"Carta #{id}";
                    await _notificationService.NotifyFavoriteCardChangedAsync(id, cardName, "removed");
                }
                catch (Exception notifEx)
                {
                    _logger.LogWarning(notifEx, "Errore invio notifica preferiti per carta eliminata {CardId}", id);
                }

                _cardRepository.Delete(card);
                await _cardRepository.SaveChangesAsync();

                _logger.LogInformation("Carta eliminata: {CardId}", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'eliminazione della carta {CardId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        [HttpGet("{id}/photos")]
        [RequirePermission("CARDS.READ.ALL")]
        public async Task<ActionResult<IEnumerable<CardPhotoDto>>> GetCardPhotos(int id)
        {
            var card = await _cardRepository.GetByIdAsync(id);
            if (card == null || card.IsDeleted)
                return NotFound(new { message = $"Carta con ID {id} non trovata" });

            var photos = await _context.CardPhotos
                .Where(p => p.CardId == id && !p.IsDeleted)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new CardPhotoDto
                {
                    Id = p.Id,
                    CardId = p.CardId,
                    UploadedByUserId = p.UploadedByUserId,
                    ContentType = p.ContentType,
                    FileSizeBytes = p.FileSizeBytes,
                    CreatedAt = p.CreatedAt,
                    DownloadUrl = $"/api/cards/photos/{p.Id}/binary"
                })
                .ToListAsync();

            return Ok(new
            {
                cardId = id,
                count = photos.Count,
                photos
            });
        }

        [HttpPost("{id}/photos")]
        [RequirePermission("CARDS.UPDATE.OWN", "CARDS.UPDATE.ANY")]
        public async Task<ActionResult<CardPhotoDto>> UploadCardPhoto(int id, [FromBody] UploadCardPhotoRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var card = await _cardRepository.GetByIdAsync(id);
            if (card == null || card.IsDeleted)
                return NotFound(new { message = $"Carta con ID {id} non trovata" });

            if (card.UserId != userId)
                return Forbid();

            var allowedContentTypes = new[] { "image/webp", "image/jpeg", "image/png" };
            if (!allowedContentTypes.Contains(request.ContentType))
                return BadRequest(new { message = "Formato immagine non supportato. Usa webp, jpeg o png." });

            byte[] imageBytes;
            try
            {
                imageBytes = Convert.FromBase64String(request.Base64Image);
            }
            catch
            {
                return BadRequest(new { message = "Immagine Base64 non valida" });
            }

            const int maxPhotoSizeBytes = 350 * 1024;
            if (imageBytes.Length > maxPhotoSizeBytes)
                return BadRequest(new { message = "Immagine troppo grande. Massimo 350KB." });

            var photo = new CardPhoto
            {
                CardId = id,
                UploadedByUserId = userId,
                ContentType = request.ContentType,
                ImageData = imageBytes,
                FileSizeBytes = imageBytes.Length
            };

            _context.CardPhotos.Add(photo);
            await _context.SaveChangesAsync();

            return Ok(new CardPhotoDto
            {
                Id = photo.Id,
                CardId = photo.CardId,
                UploadedByUserId = photo.UploadedByUserId,
                ContentType = photo.ContentType,
                FileSizeBytes = photo.FileSizeBytes,
                CreatedAt = photo.CreatedAt,
                DownloadUrl = $"/api/cards/photos/{photo.Id}/binary"
            });
        }

        [HttpGet("photos/{photoId}/binary")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCardPhotoBinary(int photoId)
        {
            var photo = await _context.CardPhotos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == photoId && !p.IsDeleted);

            if (photo == null)
                return NotFound(new { message = "Foto non trovata" });

            return File(photo.ImageData, photo.ContentType);
        }

        [HttpDelete("{id}/photos/{photoId}")]
        [RequirePermission("CARDS.UPDATE.OWN", "CARDS.UPDATE.ANY")]
        public async Task<IActionResult> DeleteCardPhoto(int id, int photoId)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var card = await _cardRepository.GetByIdAsync(id);
            if (card == null || card.IsDeleted)
                return NotFound(new { message = $"Carta con ID {id} non trovata" });

            if (card.UserId != userId)
                return Forbid();

            var photo = await _context.CardPhotos
                .FirstOrDefaultAsync(p => p.Id == photoId && p.CardId == id && !p.IsDeleted);

            if (photo == null)
                return NotFound(new { message = "Foto non trovata" });

            photo.IsDeleted = true;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>
        /// Cerca carte disponibili in un raggio specifico dalla posizione dell'utente
        /// </summary>
        [HttpGet("nearby/{userId}")]
        [RequirePermission("SEARCH.GEOGRAPHIC")]
        public async Task<ActionResult<IEnumerable<CardDto>>> GetCardsNearUser(
            int userId,
            [FromQuery] int radiusKm = 50,
            [FromQuery] string? searchTerm = null,
            [FromQuery] int? gameId = null,
            [FromQuery] int? cardSetId = null,
            [FromQuery] double? latitude = null,
            [FromQuery] double? longitude = null)
        {
            try
            {
                var user = await _userRepository.GetWithLocationAsync(userId);

                if (user == null)
                {
                    return NotFound(new { message = $"Utente con ID {userId} non trovato" });
                }

                // Usa le coordinate fornite via query, oppure quelle salvate nel profilo
                double searchLat;
                double searchLon;

                if (latitude.HasValue && longitude.HasValue)
                {
                    searchLat = latitude.Value;
                    searchLon = longitude.Value;
                }
                else if (user.Location != null && user.Location.Latitude.HasValue && user.Location.Longitude.HasValue)
                {
                    searchLat = (double)user.Location.Latitude.Value;
                    searchLon = (double)user.Location.Longitude.Value;
                }
                else
                {
                    return BadRequest(new { message = "L'utente non ha una location configurata. Fornire latitude e longitude come parametri oppure salvare la posizione nel profilo." });
                }

                if (radiusKm < 1 || radiusKm > 1000)
                {
                    return BadRequest(new { message = "Il raggio deve essere tra 1 e 1000 km" });
                }

                // Ottieni tutte le carte disponibili
                var allCards = await _cardRepository.GetAvailableCardsAsync();

                // Escludi le proprie carte
                allCards = allCards.Where(c => c.UserId != userId);

                // Filtra per termine di ricerca se fornito
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var lowerSearchTerm = searchTerm.ToLower();
                    allCards = allCards.Where(c =>
                        c.CardInfo.Name.ToLower().Contains(lowerSearchTerm) ||
                        c.CardInfo.CardSet.Name.ToLower().Contains(lowerSearchTerm) ||
                        c.CardInfo.CardSet.Game.Name.ToLower().Contains(lowerSearchTerm));
                }

                // Filtra per gioco se fornito
                if (gameId.HasValue)
                {
                    allCards = allCards.Where(c => c.CardInfo.CardSet.GameId == gameId.Value);
                }

                // Filtra per set se fornito
                if (cardSetId.HasValue)
                {
                    allCards = allCards.Where(c => c.CardInfo.CardSetId == cardSetId.Value);
                }

                // Filtra per distanza
                var cardsInRadius = new List<(Card card, double distance)>();

                foreach (var card in allCards)
                {
                    if (card.User?.Location != null &&
                        card.User.Location.Latitude.HasValue &&
                        card.User.Location.Longitude.HasValue)
                    {
                        var distance = CalculateDistance(
                            searchLat,
                            searchLon,
                            (double)card.User.Location.Latitude.Value,
                            (double)card.User.Location.Longitude.Value
                        );

                        if (distance <= radiusKm)
                        {
                            cardsInRadius.Add((card, distance));
                        }
                    }
                }

                // Ordina per distanza
                var sortedCards = cardsInRadius
                    .OrderBy(x => x.distance)
                    .Select(x => x.card);

                var cardDtos = sortedCards.Select(MapToDto);

                return Ok(new
                {
                    userId,
                    username = user.Username,
                    searchLocation = new
                    {
                        latitude = searchLat,
                        longitude = searchLon
                    },
                    radiusKm,
                    searchTerm,
                    gameId,
                    cardSetId,
                    count = cardDtos.Count(),
                    cards = cardDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la ricerca carte nelle vicinanze dell'utente {UserId}", userId);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthRadiusKm = 6371;

            var dLat = DegreesToRadians(lat2 - lat1);
            var dLon = DegreesToRadians(lon2 - lon1);

            lat1 = DegreesToRadians(lat1);
            lat2 = DegreesToRadians(lat2);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2) * Math.Cos(lat1) * Math.Cos(lat2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return earthRadiusKm * c;
        }

        private static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }

        private static CardDto MapToDto(Card card)
        {
            return new CardDto
            {
                Id = card.Id,
                UserId = card.UserId,
                UserUsername = card.User?.Username ?? string.Empty,
                CardInfoId = card.CardInfoId,
                CardName = card.CardInfo?.Name ?? string.Empty,
                CardSetName = card.CardInfo?.CardSet?.Name ?? string.Empty,
                GameName = card.CardInfo?.CardSet?.Game?.Name ?? string.Empty,
                CardNumber = card.CardInfo?.CardNumber,
                Rarity = card.CardInfo?.Rarity,
                Condition = card.Condition.ToString(),
                Notes = card.Notes,
                IsAvailableForTrade = card.IsAvailableForTrade,
                EstimatedValue = card.EstimatedValue,
                ImageSmall = card.CardInfo?.ImageSmall,
                ImageNormal = card.CardInfo?.ImageNormal ?? card.CardInfo?.ImageUrl,
                ImageLarge = card.CardInfo?.ImageLarge,
                Quantity = card.Quantity,
                HasUserPhotos = card.Photos?.Any(p => !p.IsDeleted) == true,
                UserPhotoCount = card.Photos?.Count(p => !p.IsDeleted) ?? 0,
                CreatedAt = card.CreatedAt,
                UserLocation = card.User?.Location != null ? new UserLocationDto
                {
                    City = card.User.Location.City,
                    Province = card.User.Location.Province,
                    Country = card.User.Location.Country,
                    PostalCode = card.User.Location.PostalCode,
                    Latitude = card.User.Location.Latitude,
                    Longitude = card.User.Location.Longitude,
                    MaxDistanceKm = card.User.Location.MaxDistanceKm
                } : null
            };
        }

        private async Task<CardDetailDto> MapToDetailDtoAsync(Card card)
        {
            var ci = card.CardInfo;
            var photos = await _context.CardPhotos
                .Where(p => p.CardId == card.Id && !p.IsDeleted)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new CardPhotoDto
                {
                    Id = p.Id,
                    CardId = p.CardId,
                    UploadedByUserId = p.UploadedByUserId,
                    ContentType = p.ContentType,
                    FileSizeBytes = p.FileSizeBytes,
                    CreatedAt = p.CreatedAt,
                    DownloadUrl = $"/api/cards/photos/{p.Id}/binary"
                })
                .ToListAsync();

            return new CardDetailDto
            {
                Id = card.Id,
                UserId = card.UserId,
                UserUsername = card.User?.Username ?? string.Empty,
                CardInfoId = card.CardInfoId,
                CardName = ci?.Name ?? string.Empty,
                CardSetName = ci?.CardSet?.Name ?? string.Empty,
                GameName = ci?.CardSet?.Game?.Name ?? string.Empty,
                CardNumber = ci?.CardNumber,
                Rarity = ci?.Rarity,
                CardType = ci?.Type,
                CardDescription = ci?.Description,
                ImageUrl = ci?.ImageUrl,
                Condition = card.Condition.ToString(),
                Quantity = card.Quantity,
                Notes = card.Notes,
                IsAvailableForTrade = card.IsAvailableForTrade,
                EstimatedValue = card.EstimatedValue,
                HasUserPhotos = photos.Count > 0,
                UserPhotoCount = photos.Count,
                Photos = photos,
                CreatedAt = card.CreatedAt,
                // Campi Scryfall
                ScryfallId = ci?.ScryfallId,
                ManaCost = ci?.ManaCost,
                Cmc = ci?.Cmc,
                TypeLine = ci?.TypeLine,
                OracleText = ci?.OracleText,
                Colors = ci?.Colors,
                Power = ci?.Power,
                Toughness = ci?.Toughness,
                Loyalty = ci?.Loyalty,
                Artist = ci?.Artist,
                Keywords = ci?.Keywords,
                ScryfallUri = ci?.ScryfallUri,
                Images = ci?.ImageSmall != null ? new CardImagesDto
                {
                    Small = ci.ImageSmall,
                    Normal = ci.ImageNormal,
                    Large = ci.ImageLarge,
                    Png = ci.ImagePng,
                    ArtCrop = ci.ImageArtCrop,
                    BorderCrop = ci.ImageBorderCrop
                } : null,
                Prices = ci?.PriceUsd != null || ci?.PriceEur != null ? new CardPricesDto
                {
                    Usd = ci.PriceUsd,
                    UsdFoil = ci.PriceUsdFoil,
                    Eur = ci.PriceEur,
                    EurFoil = ci.PriceEurFoil
                } : null,
                UserLocation = card.User?.Location != null ? new UserLocationDto
                {
                    City = card.User.Location.City,
                    Province = card.User.Location.Province,
                    Country = card.User.Location.Country,
                    PostalCode = card.User.Location.PostalCode,
                    Latitude = card.User.Location.Latitude,
                    Longitude = card.User.Location.Longitude,
                    MaxDistanceKm = card.User.Location.MaxDistanceKm
                } : null
            };
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }
    }
}