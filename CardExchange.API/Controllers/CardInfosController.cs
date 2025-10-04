using CardExchange.API.DTOs.Requests;
using CardExchange.API.DTOs.Responses;
using CardExchange.Core.Entities;
using CardExchange.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CardExchange.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CardInfosController : ControllerBase
    {
        private readonly ICardInfoRepository _cardInfoRepository;
        private readonly IBaseRepository<CardSet> _cardSetRepository;
        private readonly IBaseRepository<Game> _gameRepository;
        private readonly ICardRepository _cardRepository;
        private readonly IWishlistRepository _wishlistRepository;
        private readonly ILogger<CardInfosController> _logger;

        public CardInfosController(
            ICardInfoRepository cardInfoRepository,
            IBaseRepository<CardSet> cardSetRepository,
            IBaseRepository<Game> gameRepository,
            ICardRepository cardRepository,
            IWishlistRepository wishlistRepository,
            ILogger<CardInfosController> logger)
        {
            _cardInfoRepository = cardInfoRepository;
            _cardSetRepository = cardSetRepository;
            _gameRepository = gameRepository;
            _cardRepository = cardRepository;
            _wishlistRepository = wishlistRepository;
            _logger = logger;
        }

        /// <summary>
        /// Ottiene tutte le informazioni delle carte
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CardInfoDto>>> GetAllCardInfos()
        {
            try
            {
                var cardInfos = await _cardInfoRepository.GetAllAsync();

                // Ottieni tutti i CardSet necessari
                var cardSetIds = cardInfos.Select(ci => ci.CardSetId).Distinct();
                var cardSets = new Dictionary<int, (string Name, string Code, int GameId)>();
                var games = new Dictionary<int, string>();

                foreach (var cardSetId in cardSetIds)
                {
                    var cardSet = await _cardSetRepository.GetByIdAsync(cardSetId);
                    if (cardSet != null)
                    {
                        cardSets[cardSetId] = (cardSet.Name, cardSet.Code, cardSet.GameId);

                        if (!games.ContainsKey(cardSet.GameId))
                        {
                            var game = await _gameRepository.GetByIdAsync(cardSet.GameId);
                            if (game != null)
                            {
                                games[cardSet.GameId] = game.Name;
                            }
                        }
                    }
                }

                var cardInfoDtos = cardInfos.Select(ci => MapToDto(ci, cardSets, games));

                return Ok(new
                {
                    count = cardInfoDtos.Count(),
                    cardInfos = cardInfoDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero delle CardInfo");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Ottiene una CardInfo per ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<CardInfoDto>> GetCardInfoById(int id)
        {
            try
            {
                var cardInfo = await _cardInfoRepository.GetByIdAsync(id);

                if (cardInfo == null)
                {
                    return NotFound(new { message = $"CardInfo con ID {id} non trovata" });
                }

                var cardSet = await _cardSetRepository.GetByIdAsync(cardInfo.CardSetId);
                var game = cardSet != null ? await _gameRepository.GetByIdAsync(cardSet.GameId) : null;

                var cardSets = new Dictionary<int, (string Name, string Code, int GameId)>
                {
                    [cardInfo.CardSetId] = (cardSet?.Name ?? "Unknown", cardSet?.Code ?? "", cardSet?.GameId ?? 0)
                };

                var games = new Dictionary<int, string>();
                if (game != null && cardSet != null)
                {
                    games[cardSet.GameId] = game.Name;
                }

                return Ok(MapToDto(cardInfo, cardSets, games));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero della CardInfo {CardInfoId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Ottiene una CardInfo con statistiche dettagliate
        /// </summary>
        [HttpGet("{id}/details")]
        public async Task<ActionResult<CardInfoDetailDto>> GetCardInfoDetails(int id)
        {
            try
            {
                var cardInfo = await _cardInfoRepository.GetByIdAsync(id);

                if (cardInfo == null)
                {
                    return NotFound(new { message = $"CardInfo con ID {id} non trovata" });
                }

                var cardSet = await _cardSetRepository.GetByIdAsync(cardInfo.CardSetId);
                var game = cardSet != null ? await _gameRepository.GetByIdAsync(cardSet.GameId) : null;

                // Conta le carte nelle collezioni
                var cardsInCollections = await _cardRepository.FindAsync(c => c.CardInfoId == id);
                var availableForTrade = cardsInCollections.Count(c => c.IsAvailableForTrade);

                // Conta le carte nelle wishlist
                var wishlistItems = await _wishlistRepository.FindAsync(wi => wi.CardInfoId == id);

                return Ok(MapToDetailDto(
                    cardInfo,
                    cardSet?.Name ?? "Unknown",
                    cardSet?.Code ?? "",
                    game?.Name ?? "Unknown",
                    cardsInCollections.Count(),
                    availableForTrade,
                    wishlistItems.Count()
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero dei dettagli della CardInfo {CardInfoId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Ottiene tutte le carte di un set specifico
        /// </summary>
        [HttpGet("by-cardset/{cardSetId}")]
        public async Task<ActionResult<IEnumerable<CardInfoDto>>> GetCardInfosByCardSet(int cardSetId)
        {
            try
            {
                var cardSet = await _cardSetRepository.GetByIdAsync(cardSetId);
                if (cardSet == null)
                {
                    return NotFound(new { message = $"CardSet con ID {cardSetId} non trovato" });
                }

                var game = await _gameRepository.GetByIdAsync(cardSet.GameId);
                var cardInfos = await _cardInfoRepository.GetByCardSetAsync(cardSetId);

                var cardSets = new Dictionary<int, (string Name, string Code, int GameId)>
                {
                    [cardSetId] = (cardSet.Name, cardSet.Code, cardSet.GameId)
                };

                var games = new Dictionary<int, string>
                {
                    [cardSet.GameId] = game?.Name ?? "Unknown"
                };

                var cardInfoDtos = cardInfos.Select(ci => MapToDto(ci, cardSets, games));

                return Ok(new
                {
                    cardSetId,
                    cardSetName = cardSet.Name,
                    cardSetCode = cardSet.Code,
                    gameName = game?.Name ?? "Unknown",
                    count = cardInfoDtos.Count(),
                    cardInfos = cardInfoDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero delle CardInfo per il set {CardSetId}", cardSetId);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Cerca carte per nome
        /// </summary>
        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<CardInfoDto>>> SearchCardInfos([FromQuery] string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return BadRequest(new { message = "Il nome è obbligatorio" });
                }

                if (name.Length < 2)
                {
                    return BadRequest(new { message = "Il nome deve essere di almeno 2 caratteri" });
                }

                var cardInfos = await _cardInfoRepository.SearchByNameAsync(name);

                // Ottieni tutti i CardSet necessari
                var cardSetIds = cardInfos.Select(ci => ci.CardSetId).Distinct();
                var cardSets = new Dictionary<int, (string Name, string Code, int GameId)>();
                var games = new Dictionary<int, string>();

                foreach (var cardSetId in cardSetIds)
                {
                    var cardSet = await _cardSetRepository.GetByIdAsync(cardSetId);
                    if (cardSet != null)
                    {
                        cardSets[cardSetId] = (cardSet.Name, cardSet.Code, cardSet.GameId);

                        if (!games.ContainsKey(cardSet.GameId))
                        {
                            var game = await _gameRepository.GetByIdAsync(cardSet.GameId);
                            if (game != null)
                            {
                                games[cardSet.GameId] = game.Name;
                            }
                        }
                    }
                }

                var cardInfoDtos = cardInfos.Select(ci => MapToDto(ci, cardSets, games));

                return Ok(new
                {
                    searchTerm = name,
                    count = cardInfoDtos.Count(),
                    cardInfos = cardInfoDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la ricerca delle CardInfo con nome: {Name}", name);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Ottiene le carte più popolari
        /// </summary>
        [HttpGet("popular")]
        public async Task<ActionResult<IEnumerable<CardInfoDto>>> GetPopularCards([FromQuery] int count = 10)
        {
            try
            {
                if (count < 1 || count > 100)
                {
                    return BadRequest(new { message = "Il conteggio deve essere tra 1 e 100" });
                }

                var cardInfos = await _cardInfoRepository.GetPopularCardsAsync(count);

                // Ottieni tutti i CardSet necessari
                var cardSetIds = cardInfos.Select(ci => ci.CardSetId).Distinct();
                var cardSets = new Dictionary<int, (string Name, string Code, int GameId)>();
                var games = new Dictionary<int, string>();

                foreach (var cardSetId in cardSetIds)
                {
                    var cardSet = await _cardSetRepository.GetByIdAsync(cardSetId);
                    if (cardSet != null)
                    {
                        cardSets[cardSetId] = (cardSet.Name, cardSet.Code, cardSet.GameId);

                        if (!games.ContainsKey(cardSet.GameId))
                        {
                            var game = await _gameRepository.GetByIdAsync(cardSet.GameId);
                            if (game != null)
                            {
                                games[cardSet.GameId] = game.Name;
                            }
                        }
                    }
                }

                var cardInfoDtos = cardInfos.Select(ci => MapToDto(ci, cardSets, games));

                return Ok(new
                {
                    count = cardInfoDtos.Count(),
                    cardInfos = cardInfoDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero delle carte popolari");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Crea una nuova CardInfo
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<CardInfoDto>> CreateCardInfo([FromBody] CreateCardInfoRequest request)
        {
            try
            {
                var cardSet = await _cardSetRepository.GetByIdAsync(request.CardSetId);
                if (cardSet == null)
                {
                    return NotFound(new { message = $"CardSet con ID {request.CardSetId} non trovato" });
                }

                var cardInfo = new CardInfo
                {
                    CardSetId = request.CardSetId,
                    Name = request.Name,
                    CardNumber = request.CardNumber,
                    Rarity = request.Rarity,
                    Type = request.Type,
                    Description = request.Description,
                    ImageUrl = request.ImageUrl
                };

                await _cardInfoRepository.AddAsync(cardInfo);
                await _cardInfoRepository.SaveChangesAsync();

                _logger.LogInformation("CardInfo creata: {CardInfoId} - {CardInfoName}", cardInfo.Id, cardInfo.Name);

                var game = await _gameRepository.GetByIdAsync(cardSet.GameId);

                var cardSets = new Dictionary<int, (string Name, string Code, int GameId)>
                {
                    [request.CardSetId] = (cardSet.Name, cardSet.Code, cardSet.GameId)
                };

                var games = new Dictionary<int, string>
                {
                    [cardSet.GameId] = game?.Name ?? "Unknown"
                };

                return CreatedAtAction(nameof(GetCardInfoById), new { id = cardInfo.Id }, MapToDto(cardInfo, cardSets, games));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la creazione della CardInfo");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Aggiorna una CardInfo esistente
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<CardInfoDto>> UpdateCardInfo(int id, [FromBody] UpdateCardInfoRequest request)
        {
            try
            {
                var cardInfo = await _cardInfoRepository.GetByIdAsync(id);

                if (cardInfo == null)
                {
                    return NotFound(new { message = $"CardInfo con ID {id} non trovata" });
                }

                if (!string.IsNullOrWhiteSpace(request.Name))
                    cardInfo.Name = request.Name;

                if (request.CardNumber != null)
                    cardInfo.CardNumber = request.CardNumber;

                if (request.Rarity != null)
                    cardInfo.Rarity = request.Rarity;

                if (request.Type != null)
                    cardInfo.Type = request.Type;

                if (request.Description != null)
                    cardInfo.Description = request.Description;

                if (request.ImageUrl != null)
                    cardInfo.ImageUrl = request.ImageUrl;

                _cardInfoRepository.Update(cardInfo);
                await _cardInfoRepository.SaveChangesAsync();

                _logger.LogInformation("CardInfo aggiornata: {CardInfoId}", id);

                var cardSet = await _cardSetRepository.GetByIdAsync(cardInfo.CardSetId);
                var game = cardSet != null ? await _gameRepository.GetByIdAsync(cardSet.GameId) : null;

                var cardSets = new Dictionary<int, (string Name, string Code, int GameId)>
                {
                    [cardInfo.CardSetId] = (cardSet?.Name ?? "Unknown", cardSet?.Code ?? "", cardSet?.GameId ?? 0)
                };

                var games = new Dictionary<int, string>();
                if (game != null && cardSet != null)
                {
                    games[cardSet.GameId] = game.Name;
                }

                return Ok(MapToDto(cardInfo, cardSets, games));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'aggiornamento della CardInfo {CardInfoId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Elimina una CardInfo (soft delete)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCardInfo(int id)
        {
            try
            {
                var cardInfo = await _cardInfoRepository.GetByIdAsync(id);

                if (cardInfo == null)
                {
                    return NotFound(new { message = $"CardInfo con ID {id} non trovata" });
                }

                _cardInfoRepository.Delete(cardInfo);
                await _cardInfoRepository.SaveChangesAsync();

                _logger.LogInformation("CardInfo eliminata: {CardInfoId}", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'eliminazione della CardInfo {CardInfoId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        // Helper methods
        // Helper methods
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
                Quantity = card.Quantity,  // ← AGGIUNTO
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
                Quantity = card.Quantity,  // ← AGGIUNTO
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