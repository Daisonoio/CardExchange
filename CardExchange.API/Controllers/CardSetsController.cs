using CardExchange.API.DTOs.Requests;
using CardExchange.API.DTOs.Responses;
using CardExchange.Core.Entities;
using CardExchange.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CardExchange.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CardSetsController : ControllerBase
    {
        private readonly IBaseRepository<CardSet> _cardSetRepository;
        private readonly IBaseRepository<Game> _gameRepository;
        private readonly ICardInfoRepository _cardInfoRepository;
        private readonly ILogger<CardSetsController> _logger;

        public CardSetsController(
            IBaseRepository<CardSet> cardSetRepository,
            IBaseRepository<Game> gameRepository,
            ICardInfoRepository cardInfoRepository,
            ILogger<CardSetsController> logger)
        {
            _cardSetRepository = cardSetRepository;
            _gameRepository = gameRepository;
            _cardInfoRepository = cardInfoRepository;
            _logger = logger;
        }

        /// <summary>
        /// Ottiene tutti i set di carte
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CardSetDto>>> GetAllCardSets()
        {
            try
            {
                var cardSets = await _cardSetRepository.GetAllAsync();

                // Carica i giochi per avere i nomi
                var gameIds = cardSets.Select(cs => cs.GameId).Distinct();
                var games = new Dictionary<int, string>();

                foreach (var gameId in gameIds)
                {
                    var game = await _gameRepository.GetByIdAsync(gameId);
                    if (game != null)
                    {
                        games[gameId] = game.Name;
                    }
                }

                var cardSetDtos = cardSets.Select(cs => MapToDto(cs, games.GetValueOrDefault(cs.GameId, "Unknown")));

                return Ok(new
                {
                    count = cardSetDtos.Count(),
                    cardSets = cardSetDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero dei card set");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Ottiene un set di carte per ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<CardSetDto>> GetCardSetById(int id)
        {
            try
            {
                var cardSet = await _cardSetRepository.GetByIdAsync(id);

                if (cardSet == null)
                {
                    return NotFound(new { message = $"CardSet con ID {id} non trovato" });
                }

                var game = await _gameRepository.GetByIdAsync(cardSet.GameId);

                return Ok(MapToDto(cardSet, game?.Name ?? "Unknown"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero del CardSet {CardSetId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Ottiene un set di carte con tutte le sue carte
        /// </summary>
        [HttpGet("{id}/details")]
        public async Task<ActionResult<CardSetDetailDto>> GetCardSetDetails(int id)
        {
            try
            {
                var cardSet = await _cardSetRepository.GetByIdAsync(id);

                if (cardSet == null)
                {
                    return NotFound(new { message = $"CardSet con ID {id} non trovato" });
                }

                var game = await _gameRepository.GetByIdAsync(cardSet.GameId);
                var cardInfos = await _cardInfoRepository.GetByCardSetAsync(id);

                return Ok(MapToDetailDto(cardSet, game?.Name ?? "Unknown", cardInfos));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero dei dettagli del CardSet {CardSetId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Ottiene tutti i set di un gioco specifico
        /// </summary>
        [HttpGet("by-game/{gameId}")]
        public async Task<ActionResult<IEnumerable<CardSetDto>>> GetCardSetsByGame(int gameId)
        {
            try
            {
                var game = await _gameRepository.GetByIdAsync(gameId);
                if (game == null)
                {
                    return NotFound(new { message = $"Gioco con ID {gameId} non trovato" });
                }

                var cardSets = await _cardSetRepository.FindAsync(cs => cs.GameId == gameId);
                var cardSetDtos = cardSets.Select(cs => MapToDto(cs, game.Name));

                return Ok(new
                {
                    gameId,
                    gameName = game.Name,
                    count = cardSetDtos.Count(),
                    cardSets = cardSetDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero dei CardSet per il gioco {GameId}", gameId);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Crea un nuovo set di carte
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<CardSetDto>> CreateCardSet([FromBody] CreateCardSetRequest request)
        {
            try
            {
                var game = await _gameRepository.GetByIdAsync(request.GameId);
                if (game == null)
                {
                    return NotFound(new { message = $"Gioco con ID {request.GameId} non trovato" });
                }

                var cardSet = new CardSet
                {
                    GameId = request.GameId,
                    Name = request.Name,
                    Code = request.Code,
                    ReleaseDate = request.ReleaseDate,
                    Description = request.Description,
                    IsActive = true
                };

                await _cardSetRepository.AddAsync(cardSet);
                await _cardSetRepository.SaveChangesAsync();

                _logger.LogInformation("CardSet creato: {CardSetId} - {CardSetName}", cardSet.Id, cardSet.Name);

                return CreatedAtAction(nameof(GetCardSetById), new { id = cardSet.Id }, MapToDto(cardSet, game.Name));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la creazione del CardSet");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Aggiorna un set di carte esistente
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<CardSetDto>> UpdateCardSet(int id, [FromBody] UpdateCardSetRequest request)
        {
            try
            {
                var cardSet = await _cardSetRepository.GetByIdAsync(id);

                if (cardSet == null)
                {
                    return NotFound(new { message = $"CardSet con ID {id} non trovato" });
                }

                if (!string.IsNullOrWhiteSpace(request.Name))
                    cardSet.Name = request.Name;

                if (!string.IsNullOrWhiteSpace(request.Code))
                    cardSet.Code = request.Code;

                if (request.ReleaseDate.HasValue)
                    cardSet.ReleaseDate = request.ReleaseDate;

                if (request.Description != null)
                    cardSet.Description = request.Description;

                if (request.IsActive.HasValue)
                    cardSet.IsActive = request.IsActive.Value;

                _cardSetRepository.Update(cardSet);
                await _cardSetRepository.SaveChangesAsync();

                _logger.LogInformation("CardSet aggiornato: {CardSetId}", id);

                var game = await _gameRepository.GetByIdAsync(cardSet.GameId);
                return Ok(MapToDto(cardSet, game?.Name ?? "Unknown"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'aggiornamento del CardSet {CardSetId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Elimina un set di carte (soft delete)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCardSet(int id)
        {
            try
            {
                var cardSet = await _cardSetRepository.GetByIdAsync(id);

                if (cardSet == null)
                {
                    return NotFound(new { message = $"CardSet con ID {id} non trovato" });
                }

                _cardSetRepository.Delete(cardSet);
                await _cardSetRepository.SaveChangesAsync();

                _logger.LogInformation("CardSet eliminato: {CardSetId}", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'eliminazione del CardSet {CardSetId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        // Helper methods
        private static CardSetDto MapToDto(CardSet cardSet, string gameName)
        {
            return new CardSetDto
            {
                Id = cardSet.Id,
                GameId = cardSet.GameId,
                GameName = gameName,
                Name = cardSet.Name,
                Code = cardSet.Code,
                ReleaseDate = cardSet.ReleaseDate,
                Description = cardSet.Description,
                IsActive = cardSet.IsActive,
                CreatedAt = cardSet.CreatedAt
            };
        }

        private static CardSetDetailDto MapToDetailDto(CardSet cardSet, string gameName, IEnumerable<CardInfo> cardInfos)
        {
            return new CardSetDetailDto
            {
                Id = cardSet.Id,
                GameId = cardSet.GameId,
                GameName = gameName,
                Name = cardSet.Name,
                Code = cardSet.Code,
                ReleaseDate = cardSet.ReleaseDate,
                Description = cardSet.Description,
                IsActive = cardSet.IsActive,
                CreatedAt = cardSet.CreatedAt,
                CardInfosCount = cardInfos.Count(),
                CardInfos = cardInfos.Select(ci => new CardInfoDto
                {
                    Id = ci.Id,
                    CardSetId = ci.CardSetId,
                    CardSetName = cardSet.Name,
                    CardSetCode = cardSet.Code,
                    GameName = gameName,
                    Name = ci.Name,
                    CardNumber = ci.CardNumber,
                    Rarity = ci.Rarity,
                    Type = ci.Type,
                    Description = ci.Description,
                    ImageUrl = ci.ImageUrl,
                    CreatedAt = ci.CreatedAt
                })
            };
        }
    }
}