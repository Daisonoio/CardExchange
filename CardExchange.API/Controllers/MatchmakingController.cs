using CardExchange.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardExchange.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MatchmakingController : ControllerBase
    {
        private readonly IMatchmakingService _matchmakingService;
        private readonly ILogger<MatchmakingController> _logger;

        public MatchmakingController(
            IMatchmakingService matchmakingService,
            ILogger<MatchmakingController> logger)
        {
            _matchmakingService = matchmakingService;
            _logger = logger;
        }

        /// <summary>
        /// Trova match intelligenti per scambi basati su wishlist bidirezionale e vicinanza
        /// </summary>
        [HttpGet]
        public async Task<ActionResult> GetMatches(
            [FromQuery] int? radiusKm = null,
            [FromQuery] double? latitude = null,
            [FromQuery] double? longitude = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0) return Unauthorized();

                var matches = await _matchmakingService.FindMatchesAsync(userId, radiusKm, latitude, longitude);
                var matchList = matches.ToList();

                var mutualCount = matchList.Count(m => m.IsMutual);

                return Ok(new
                {
                    totalMatches = matchList.Count,
                    mutualMatches = mutualCount,
                    matches = matchList.Select(m => new
                    {
                        m.UserId,
                        m.Username,
                        m.AvatarUrl,
                        m.ReputationScore,
                        m.TotalTradesCompleted,
                        m.City,
                        m.Province,
                        m.DistanceKm,
                        m.Score,
                        m.MutualCardCount,
                        m.IsMutual,
                        theyHaveIWant = m.TheyHaveIWant.Select(c => new
                        {
                            c.CardId,
                            c.CardInfoId,
                            c.Name,
                            c.SetName,
                            c.ImageSmall,
                            c.Condition,
                            c.Quantity,
                            c.PriceEur,
                            c.WishlistPriority
                        }),
                        iHaveTheyWant = m.IHaveTheyWant.Select(c => new
                        {
                            c.CardId,
                            c.CardInfoId,
                            c.Name,
                            c.SetName,
                            c.ImageSmall,
                            c.Condition,
                            c.Quantity,
                            c.PriceEur,
                            c.WishlistPriority
                        })
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il matchmaking");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }
    }
}
