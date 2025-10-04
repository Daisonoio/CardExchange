using CardExchange.API.DTOs.Requests;
using CardExchange.API.DTOs.Responses;
using CardExchange.Core.Entities;
using CardExchange.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CardExchange.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GamesController : ControllerBase
    {
        private readonly IBaseRepository<Game> _gameRepository;
        private readonly IBaseRepository<CardSet> _cardSetRepository;
        private readonly ILogger<GamesController> _logger;

        public GamesController(
            IBaseRepository<Game> gameRepository,
            IBaseRepository<CardSet> cardSetRepository,
            ILogger<GamesController> logger)
        {
            _gameRepository = gameRepository;
            _cardSetRepository = cardSetRepository;
            _logger = logger;
        }

        /// <summary>
        /// Ottiene tutti i giochi
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<GameDto>>> GetAllGames()
        {
            try
            {
                var games = await _gameRepository.GetAllAsync();
                var gameDtos = games.Select(MapToDto);

                return Ok(new
                {
                    count = gameDtos.Count(),
                    games = gameDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero dei giochi");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Ottiene un gioco per ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<GameDto>> GetGameById(int id)
        {
            try
            {
                var game = await _gameRepository.GetByIdAsync(id);

                if (game == null)
                {
                    return NotFound(new { message = $"Gioco con ID {id} non trovato" });
                }

                return Ok(MapToDto(game));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero del gioco {GameId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Ottiene un gioco con tutti i suoi set
        /// </summary>
        [HttpGet("{id}/details")]
        public async Task<ActionResult<GameDetailDto>> GetGameDetails(int id)
        {
            try
            {
                var game = await _gameRepository.GetByIdAsync(id);

                if (game == null)
                {
                    return NotFound(new { message = $"Gioco con ID {id} non trovato" });
                }

                var cardSets = await _cardSetRepository.FindAsync(cs => cs.GameId == id);

                return Ok(MapToDetailDto(game, cardSets));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero dei dettagli del gioco {GameId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Crea un nuovo gioco
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<GameDto>> CreateGame([FromBody] CreateGameRequest request)
        {
            try
            {
                var game = new Game
                {
                    Name = request.Name,
                    Description = request.Description,
                    Publisher = request.Publisher,
                    IsActive = true
                };

                await _gameRepository.AddAsync(game);
                await _gameRepository.SaveChangesAsync();

                _logger.LogInformation("Gioco creato: {GameId} - {GameName}", game.Id, game.Name);

                return CreatedAtAction(nameof(GetGameById), new { id = game.Id }, MapToDto(game));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la creazione del gioco");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Aggiorna un gioco esistente
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<GameDto>> UpdateGame(int id, [FromBody] UpdateGameRequest request)
        {
            try
            {
                var game = await _gameRepository.GetByIdAsync(id);

                if (game == null)
                {
                    return NotFound(new { message = $"Gioco con ID {id} non trovato" });
                }

                if (!string.IsNullOrWhiteSpace(request.Name))
                    game.Name = request.Name;

                if (request.Description != null)
                    game.Description = request.Description;

                if (!string.IsNullOrWhiteSpace(request.Publisher))
                    game.Publisher = request.Publisher;

                if (request.IsActive.HasValue)
                    game.IsActive = request.IsActive.Value;

                _gameRepository.Update(game);
                await _gameRepository.SaveChangesAsync();

                _logger.LogInformation("Gioco aggiornato: {GameId}", id);

                return Ok(MapToDto(game));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'aggiornamento del gioco {GameId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Elimina un gioco (soft delete)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGame(int id)
        {
            try
            {
                var game = await _gameRepository.GetByIdAsync(id);

                if (game == null)
                {
                    return NotFound(new { message = $"Gioco con ID {id} non trovato" });
                }

                _gameRepository.Delete(game);
                await _gameRepository.SaveChangesAsync();

                _logger.LogInformation("Gioco eliminato: {GameId}", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'eliminazione del gioco {GameId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        // Helper methods
        private static GameDto MapToDto(Game game)
        {
            return new GameDto
            {
                Id = game.Id,
                Name = game.Name,
                Description = game.Description,
                Publisher = game.Publisher,
                IsActive = game.IsActive,
                CreatedAt = game.CreatedAt
            };
        }

        private static GameDetailDto MapToDetailDto(Game game, IEnumerable<CardSet> cardSets)
        {
            return new GameDetailDto
            {
                Id = game.Id,
                Name = game.Name,
                Description = game.Description,
                Publisher = game.Publisher,
                IsActive = game.IsActive,
                CreatedAt = game.CreatedAt,
                CardSetsCount = cardSets.Count(),
                CardSets = cardSets.Select(cs => new CardSetDto
                {
                    Id = cs.Id,
                    GameId = cs.GameId,
                    GameName = game.Name,
                    Name = cs.Name,
                    Code = cs.Code,
                    ReleaseDate = cs.ReleaseDate,
                    Description = cs.Description,
                    IsActive = cs.IsActive,
                    CreatedAt = cs.CreatedAt
                })
            };
        }
    }
}