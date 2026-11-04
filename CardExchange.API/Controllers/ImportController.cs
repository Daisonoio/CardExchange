using CardExchange.API.Services.Import;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CardExchange.API.Controllers
{
    /// <summary>
    /// Import della collezione da file CSV (Magic: ManaBox, Moxfield, Deckbox, Dragon Shield, Cardmarket, generico).
    /// </summary>
    [ApiController]
    [Route("api/import")]
    [Authorize]
    [RequestSizeLimit(4_000_000)]
    public class ImportController : ControllerBase
    {
        private readonly ICollectionImportService _import;
        private readonly ILogger<ImportController> _logger;

        public ImportController(ICollectionImportService import, ILogger<ImportController> logger)
        {
            _import = import;
            _logger = logger;
        }

        /// <summary>
        /// Analizza il CSV e abbina le righe alle carte Scryfall. Non salva nulla.
        /// </summary>
        [HttpPost("collection/preview")]
        public async Task<ActionResult<ImportPreviewResponse>> Preview([FromBody] ImportPreviewRequest request)
        {
            try
            {
                return Ok(await _import.PreviewAsync(request.Csv ?? string.Empty, request.CatalogKey));
            }
            catch (ImportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Aggiunge alla collezione dell'utente le righe confermate dall'anteprima.
        /// </summary>
        [HttpPost("collection/commit")]
        public async Task<ActionResult<ImportCommitResponse>> Commit([FromBody] ImportCommitRequest request)
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null || !int.TryParse(claim.Value, out var userId)) return Unauthorized();

            try
            {
                return Ok(await _import.CommitAsync(userId, request));
            }
            catch (ImportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
