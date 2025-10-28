using CardExchange.API.Authorization;
using CardExchange.API.DTOs.Requests;
using CardExchange.API.DTOs.Responses;
using CardExchange.Core.Entities;
using CardExchange.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        private readonly ILogger<CardsController> _logger;

        public CardsController(
            ICardRepository cardRepository,
            IUserRepository userRepository,
            ICardInfoRepository cardInfoRepository,
            ILogger<CardsController> logger)
        {
            _cardRepository = cardRepository;
            _userRepository = userRepository;
            _cardInfoRepository = cardInfoRepository;
            _logger = logger;
        }

        /// <summary>
        /// Ottiene tutte le carte disponibili per lo scambio
        /// </summary>
        [HttpGet]
        [RequirePermission("CARDS.READ.ALL")]
        public async Task<ActionResult<IEnumerable<CardDto>>> GetAllAvailableCards()
        {
            try
            {
                var cards = await _cardRepository.GetAvailableCardsAsync();
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
                var card = await _cardRepository.GetByIdAsync(id);

                if (card == null)
                {
                    return NotFound(new { message = $"Carta con ID {id} non trovata" });
                }

                // Ricarica con tutte le relazioni
                var fullCard = (await _cardRepository.FindAsync(c => c.Id == id)).FirstOrDefault();

                return Ok(MapToDetailDto(fullCard!));
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
        public async Task<ActionResult<IEnumerable<CardDto>>> GetUserCards(int userId)
        {
            try
            {
                var userExists = await _userRepository.GetByIdAsync(userId);
                if (userExists == null)
                {
                    return NotFound(new { message = $"Utente con ID {userId} non trovato" });
                }

                var cards = await _cardRepository.GetUserCardsAsync(userId);
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

                // Verifica che la CardInfo esista
                var cardInfo = await _cardInfoRepository.GetByIdAsync(request.CardInfoId);
                if (cardInfo == null)
                {
                    return NotFound(new { message = $"CardInfo con ID {request.CardInfoId} non trovata" });
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

                if (request.EstimatedValue.HasValue)
                    card.EstimatedValue = request.EstimatedValue.Value;

                _cardRepository.Update(card);
                await _cardRepository.SaveChangesAsync();

                _logger.LogInformation("Carta aggiornata: {CardId}", id);

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
            [FromQuery] int? cardSetId = null)
        {
            try
            {
                var user = await _userRepository.GetWithLocationAsync(userId);

                if (user == null)
                {
                    return NotFound(new { message = $"Utente con ID {userId} non trovato" });
                }

                if (user.Location == null || !user.Location.Latitude.HasValue || !user.Location.Longitude.HasValue)
                {
                    return BadRequest(new { message = "L'utente non ha una location configurata" });
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
                            (double)user.Location.Latitude.Value,
                            (double)user.Location.Longitude.Value,
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
                    userLocation = new
                    {
                        city = user.Location.City,
                        province = user.Location.Province,
                        country = user.Location.Country,
                        latitude = user.Location.Latitude,
                        longitude = user.Location.Longitude
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

        private static CardDetailDto MapToDetailDto(Card card)
        {
            return new CardDetailDto
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
                CardType = card.CardInfo?.Type,
                CardDescription = card.CardInfo?.Description,
                ImageUrl = card.CardInfo?.ImageUrl,
                Condition = card.Condition.ToString(),
                Notes = card.Notes,
                IsAvailableForTrade = card.IsAvailableForTrade,
                EstimatedValue = card.EstimatedValue,
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
    }
}