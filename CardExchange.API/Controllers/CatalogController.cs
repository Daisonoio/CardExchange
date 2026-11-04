using CardExchange.API.Services.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardExchange.API.Controllers
{
    /// <summary>
    /// Catalogo comune a tutti i giochi gestiti da un provider (es. Lorcana): ricerca e aggiunta di una carta.
    /// I giochi storici mantengono anche i loro endpoint dedicati.
    /// </summary>
    [ApiController]
    [Route("api/catalog")]
    [Authorize]
    public class CatalogController : ControllerBase
    {
        private readonly ICatalogRegistry _registry;
        private readonly ILogger<CatalogController> _logger;

        public CatalogController(ICatalogRegistry registry, ILogger<CatalogController> logger)
        {
            _registry = registry;
            _logger = logger;
        }

        [HttpGet("providers")]
        [AllowAnonymous]
        public IActionResult Providers() =>
            Ok(_registry.All.Select(p => new { key = p.Key, gameName = p.GameName, importHint = p.ImportHint }));

        [HttpGet("{key}/search")]
        public async Task<IActionResult> Search(string key, [FromQuery] string q, [FromQuery] int limit = 20, CancellationToken ct = default)
        {
            var provider = _registry.Get(key);
            if (provider == null) return NotFound(new { message = "Gioco non supportato" });
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2) return BadRequest(new { message = "Scrivi almeno 2 caratteri" });
            try
            {
                var cards = await provider.SearchAsync(q.Trim(), Math.Clamp(limit, 1, 50), ct);
                return Ok(new { cards });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Ricerca {Key} non riuscita", key);
                return StatusCode(StatusCodes.Status502BadGateway, new { message = "Il catalogo non risponde, riprova tra poco." });
            }
        }

        public class ImportRequest
        {
            public string ExternalId { get; set; } = string.Empty;
        }

        [HttpPost("{key}/import")]
        public async Task<IActionResult> Import(string key, [FromBody] ImportRequest request, CancellationToken ct = default)
        {
            var provider = _registry.Get(key);
            if (provider == null) return NotFound(new { message = "Gioco non supportato" });
            if (string.IsNullOrWhiteSpace(request.ExternalId) || request.ExternalId.Length > 100)
                return BadRequest(new { message = "Identificativo della carta non valido" });
            var result = await provider.ImportAsync(request.ExternalId.Trim(), ct);
            if (result == null) return NotFound(new { message = "Carta non trovata nel catalogo" });
            return Ok(new { cardInfoId = result.CardInfoId, isNew = result.IsNew });
        }
    }
}
