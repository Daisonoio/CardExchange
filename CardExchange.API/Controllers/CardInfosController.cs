using CardExchange.API.DTOs.Requests;
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
        private readonly ILogger<CardInfosController> _logger;

        public CardInfosController(
            ICardInfoRepository cardInfoRepository,
            IBaseRepository<CardSet> cardSetRepository,
            ILogger<CardInfosController> logger)
        {
            _cardInfoRepository = cardInfoRepository;
            _cardSetRepository = cardSetRepository;
            _logger = logger;
        }

        /// <summary>
        /// Ottiene tutte le informazioni delle carte
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CardInfo>>> GetAllCardInfos()
        {
            try
            {
                var cardInfos = await _cardInfoRepository.GetAllAsync();
                return Ok(cardInfos);
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
        public async Task<ActionResult<CardInfo>> GetCardInfoById(int id)
        {
            try
            {
                var cardInfo = await _cardInfoRepository.GetByIdAsync(id);

                if (cardInfo == null)
                {
                    return NotFound(new { message = $"CardInfo con ID {id} non trovata" });
                }

                return Ok(cardInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero della CardInfo {CardInfoId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Ottiene tutte le carte di un set specifico
        /// </summary>
        [HttpGet("by-cardset/{cardSetId}")]
        public async Task<ActionResult<IEnumerable<CardInfo>>> GetCardInfosByCardSet(int cardSetId)
        {
            try
            {
                var cardSetExists = await _cardSetRepository.GetByIdAsync(cardSetId);
                if (cardSetExists == null)
                {
                    return NotFound(new { message = $"CardSet con ID {cardSetId} non trovato" });
                }

                var cardInfos = await _cardInfoRepository.GetByCardSetAsync(cardSetId);
                return Ok(cardInfos);
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
        public async Task<ActionResult<IEnumerable<CardInfo>>> SearchCardInfos([FromQuery] string name)
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
                return Ok(cardInfos);
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
        public async Task<ActionResult<IEnumerable<CardInfo>>> GetPopularCards([FromQuery] int count = 10)
        {
            try
            {
                if (count < 1 || count > 100)
                {
                    return BadRequest(new { message = "Il conteggio deve essere tra 1 e 100" });
                }

                var cardInfos = await _cardInfoRepository.GetPopularCardsAsync(count);
                return Ok(cardInfos);
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
        public async Task<ActionResult<CardInfo>> CreateCardInfo([FromBody] CreateCardInfoRequest request)
        {
            try
            {
                var cardSetExists = await _cardSetRepository.GetByIdAsync(request.CardSetId);
                if (cardSetExists == null)
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

                return CreatedAtAction(nameof(GetCardInfoById), new { id = cardInfo.Id }, cardInfo);
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
        public async Task<ActionResult<CardInfo>> UpdateCardInfo(int id, [FromBody] UpdateCardInfoRequest request)
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

                return Ok(cardInfo);
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
    }

    // Request DTOs



}