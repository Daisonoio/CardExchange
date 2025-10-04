using CardExchange.API.DTOs.Requests;
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
        private readonly ILogger<CardSetsController> _logger;

        public CardSetsController(
            IBaseRepository<CardSet> cardSetRepository,
            IBaseRepository<Game> gameRepository,
            ILogger<CardSetsController> logger)
        {
            _cardSetRepository = cardSetRepository;
            _gameRepository = gameRepository;
            _logger = logger;
        }

        /// <summary>
        /// Ottiene tutti i set di carte
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CardSet>>> GetAllCardSets()
        {
            try
            {
                var cardSets = await _cardSetRepository.GetAllAsync();
                return Ok(cardSets);
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
        public async Task<ActionResult<CardSet>> GetCardSetById(int id)
        {
            try
            {
                var cardSet = await _cardSetRepository.GetByIdAsync(id);

                if (cardSet == null)
                {
                    return NotFound(new { message = $"CardSet con ID {id} non trovato" });
                }

                return Ok(cardSet);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero del CardSet {CardSetId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Ottiene tutti i set di un gioco specifico
        /// </summary>
        [HttpGet("by-game/{gameId}")]
        public async Task<ActionResult<IEnumerable<CardSet>>> GetCardSetsByGame(int gameId)
        {
            try
            {
                var gameExists = await _gameRepository.GetByIdAsync(gameId);
                if (gameExists == null)
                {
                    return NotFound(new { message = $"Gioco con ID {gameId} non trovato" });
                }

                var cardSets = await _cardSetRepository.FindAsync(cs => cs.GameId == gameId);
                return Ok(cardSets);
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
        public async Task<ActionResult<CardSet>> CreateCardSet([FromBody] CreateCardSetRequest request)
        {
            try
            {
                var gameExists = await _gameRepository.GetByIdAsync(request.GameId);
                if (gameExists == null)
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

                return CreatedAtAction(nameof(GetCardSetById), new { id = cardSet.Id }, cardSet);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la creazione del CardSet: "+ex.Message);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Aggiorna un set di carte esistente
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<CardSet>> UpdateCardSet(int id, [FromBody] UpdateCardSetRequest request)
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

                return Ok(cardSet);
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
    }

    // Request DTOs



}