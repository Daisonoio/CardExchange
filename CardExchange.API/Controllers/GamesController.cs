using CardExchange.API.DTOs.Requests;
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
        private readonly ILogger<GamesController> _logger;

        public GamesController(IBaseRepository<Game> gameRepository, ILogger<GamesController> logger)
        {
            _gameRepository = gameRepository;
            _logger = logger;
        }

        /// <summary>
        /// Ottiene tutti i giochi
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Game>>> GetAllGames()
        {
            try
            {
                var games = await _gameRepository.GetAllAsync();
                return Ok(games);
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
        public async Task<ActionResult<Game>> GetGameById(int id)
        {
            try
            {
                var game = await _gameRepository.GetByIdAsync(id);

                if (game == null)
                {
                    return NotFound(new { message = $"Gioco con ID {id} non trovato" });
                }

                return Ok(game);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero del gioco {GameId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Crea un nuovo gioco
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<Game>> CreateGame([FromBody] CreateGameRequest request)
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

                return CreatedAtAction(nameof(GetGameById), new { id = game.Id }, game);
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
        public async Task<ActionResult<Game>> UpdateGame(int id, [FromBody] UpdateGameRequest request)
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

                return Ok(game);
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
    }

    // Request DTOs



}