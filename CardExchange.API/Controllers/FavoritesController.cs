using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CardExchange.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FavoritesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public FavoritesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyFavorites()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var favorites = await _context.FavoriteCards
                .AsNoTracking()
                .Where(fc => fc.UserId == userId)
                .Include(fc => fc.Card)
                    .ThenInclude(c => c.CardInfo)
                        .ThenInclude(ci => ci.CardSet)
                .Include(fc => fc.Card)
                    .ThenInclude(c => c.User)
                        .ThenInclude(u => u!.Location)
                .Where(fc => !fc.Card.IsDeleted && fc.Card.IsAvailableForTrade)
                .OrderByDescending(fc => fc.CreatedAt)
                .Select(fc => new
                {
                    favoriteId = fc.Id,
                    fc.CardId,
                    fc.CreatedAt,
                    card = new
                    {
                        fc.Card.Id,
                        fc.Card.UserId,
                        userUsername = fc.Card.User.Username,
                        fc.Card.CardInfoId,
                        cardName = fc.Card.CardInfo.Name,
                        cardSetName = fc.Card.CardInfo.CardSet != null ? fc.Card.CardInfo.CardSet.Name : "",
                        condition = fc.Card.Condition.ToString(),
                        fc.Card.Quantity,
                        fc.Card.IsAvailableForTrade,
                        fc.Card.EstimatedValue,
                        imageSmall = fc.Card.CardInfo.ImageSmall,
                        imageNormal = fc.Card.CardInfo.ImageNormal,
                        hasUserPhotos = fc.Card.Photos.Any(p => !p.IsDeleted),
                    }
                })
                .ToListAsync();

            return Ok(new { count = favorites.Count, favorites });
        }

        [HttpPost("{cardId}")]
        public async Task<IActionResult> AddFavorite(int cardId)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var card = await _context.Cards.FindAsync(cardId);
            if (card == null || card.IsDeleted)
                return NotFound(new { message = "Carta non trovata" });

            if (card.UserId == userId)
                return BadRequest(new { message = "Non puoi aggiungere ai preferiti le tue carte" });

            var exists = await _context.FavoriteCards
                .AnyAsync(fc => fc.UserId == userId && fc.CardId == cardId);

            if (exists)
                return Conflict(new { message = "Carta già nei preferiti" });

            var favorite = new FavoriteCard
            {
                UserId = userId,
                CardId = cardId
            };

            _context.FavoriteCards.Add(favorite);
            await _context.SaveChangesAsync();

            return Ok(new { favoriteId = favorite.Id, message = "Carta aggiunta ai preferiti" });
        }

        [HttpDelete("{cardId}")]
        public async Task<IActionResult> RemoveFavorite(int cardId)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var favorite = await _context.FavoriteCards
                .FirstOrDefaultAsync(fc => fc.UserId == userId && fc.CardId == cardId);

            if (favorite == null)
                return NotFound(new { message = "Preferito non trovato" });

            favorite.IsDeleted = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Carta rimossa dai preferiti" });
        }

        [HttpGet("check/{cardId}")]
        public async Task<IActionResult> IsFavorite(int cardId)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var isFavorite = await _context.FavoriteCards
                .AnyAsync(fc => fc.UserId == userId && fc.CardId == cardId);

            return Ok(new { isFavorite });
        }

        [HttpGet("card-ids")]
        public async Task<IActionResult> GetFavoriteCardIds()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var cardIds = await _context.FavoriteCards
                .AsNoTracking()
                .Where(fc => fc.UserId == userId)
                .Select(fc => fc.CardId)
                .ToListAsync();

            return Ok(new { cardIds });
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
        }
    }
}
