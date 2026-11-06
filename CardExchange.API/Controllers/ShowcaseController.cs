using System.Security.Claims;
using System.Text.RegularExpressions;
using CardExchange.API.Authorization;
using CardExchange.API.Services.Entitlements;
using CardExchange.API.Services.Moderation;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Controllers
{
    /// <summary>
    /// Vetrina del Banco: pagina pubblica (per utenti registrati) con le carte in evidenza, il "Sono qui" in corso
    /// e il pulsante Segui; statistiche della settimana per il venditore. Prezzi solo come riferimento.
    /// </summary>
    [ApiController]
    [Authorize]
    public class ShowcaseController : ControllerBase
    {
        public const int MaxFeatured = 12;
        /// <summary>Le statistiche si mostrano da 3 persone in su (dati aggregati).</summary>
        public const int MinShown = 3;
        private static readonly Regex SlugPattern = new("^[a-z0-9](?:[a-z0-9-]{1,38})[a-z0-9]$", RegexOptions.Compiled);
        private static readonly string[] ReservedSlugs = { "admin", "api", "new", "me", "edit", "cardexchange" };

        private readonly ApplicationDbContext _context;
        private readonly IUserBlockService _blocks;

        public ShowcaseController(ApplicationDbContext context, IUserBlockService blocks)
        {
            _context = context;
            _blocks = blocks;
        }

        public record ShowcaseRequest(string Slug, string DisplayName, string? Bio, string? CoverImageUrl, List<int>? FeaturedCardIds);

        private int Me => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        private static List<int> ParseIds(string? s) =>
            (s ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => int.TryParse(x, out var i) ? i : 0).Where(i => i > 0).ToList();

        private static object CardDto(Card c) => new
        {
            id = c.Id, cardInfoId = c.CardInfoId, name = c.CardInfo.Name, setName = c.CardInfo.CardSet?.Name, number = c.CardInfo.CardNumber,
            imageUrl = c.CardInfo.ImageSmall ?? c.CardInfo.ImageNormal ?? c.CardInfo.ImageUrl,
            condition = c.Condition.ToString(), finish = c.Finish.ToString(), language = c.Language,
            price = CardPricing.EffectivePriceEur(c), priceKind = c.AskingPrice.HasValue ? "asking" : "market",
            quantity = c.Quantity
        };

        /// <summary>Vetrina pubblica. Visibile solo finché il venditore ha il piano Banco.</summary>
        [HttpGet("api/showcase/{slug}")]
        public async Task<IActionResult> Get(string slug)
        {
            var showcase = await _context.SellerShowcases.AsNoTracking().Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Slug == slug.ToLower());
            if (showcase == null || !showcase.User.IsActive) return NotFound(new { message = "Vetrina non trovata" });
            var isOwner = showcase.UserId == Me;
            if (!isOwner && (await _blocks.GetBlockedUserIdsAsync(Me)).Contains(showcase.UserId)) return NotFound(new { message = "Vetrina non trovata" });
            if (!isOwner && !await this.Entitlements(_context).HasAsync(showcase.UserId, Capability.SellerShowcase))
                return NotFound(new { message = "Vetrina non trovata" });

            var now = DateTime.UtcNow;
            if (!isOwner)
            {
                var day = DateOnly.FromDateTime(TimeZones.ToLocal(now, null));
                if (!await _context.ShowcaseVisits.AnyAsync(v => v.ShowcaseId == showcase.Id && v.ViewerId == Me && v.Day == day))
                {
                    _context.ShowcaseVisits.Add(new ShowcaseVisit { ShowcaseId = showcase.Id, ViewerId = Me, Day = day });
                    try { await _context.SaveChangesAsync(); } catch (DbUpdateException) { /* visita già contata */ }
                }
            }

            var cards = await _context.Cards.AsNoTracking().Include(c => c.CardInfo).ThenInclude(ci => ci.CardSet)
                .Where(c => c.UserId == showcase.UserId && c.IsAvailableForTrade && c.Quantity > c.ReservedQuantity)
                .OrderByDescending(c => c.CreatedAt).Take(120).ToListAsync();
            // Accesso anticipato Premium: le carte appena caricate dal banco le vedono prima i Premium
            var hidden = await EarlyAccess.HiddenAsync(this.Entitlements(_context), Me, cards.Select(c => (c.Id, c.UserId, c.CreatedAt)), now);
            cards = cards.Where(c => !hidden.Contains(c.Id)).ToList();
            var featuredIds = ParseIds(showcase.FeaturedCardIds);
            var featured = featuredIds.Select(id => cards.FirstOrDefault(c => c.Id == id)).Where(c => c != null).Select(c => CardDto(c!)).ToList();

            var live = await _context.LocationBroadcasts.AsNoTracking()
                .Where(b => b.AuthorId == showcase.UserId && b.StoreId == null && b.EndedAt == null && b.EndsAt > now)
                .OrderByDescending(b => b.CreatedAt).FirstOrDefaultAsync();
            var me = await _context.UserLocations.AsNoTracking().FirstOrDefaultAsync(l => l.UserId == Me);
            int? liveKm = live != null && me?.Latitude != null && me.Longitude != null
                ? BroadcastRules.DisplayDistanceKm(Geo.DistanceKm(me.Latitude.Value, me.Longitude.Value, live.Latitude, live.Longitude))
                : null;

            return Ok(new
            {
                showcase.Id, showcase.Slug, showcase.DisplayName, showcase.Bio, showcase.CoverImageUrl, isOwner,
                seller = new
                {
                    id = showcase.UserId, username = showcase.User.Username, avatarUrl = showcase.User.AvatarUrl,
                    reputationScore = showcase.User.ReputationScore, totalTradesCompleted = showcase.User.TotalTradesCompleted,
                    followers = await _context.Follows.CountAsync(f => f.FollowedUserId == showcase.UserId)
                },
                live = live == null ? null : new { id = live.Id, placeName = live.PlaceName, spotDetail = live.SpotDetail, endsAt = live.EndsAt, approxKm = liveKm },
                featured,
                cards = cards.Where(c => !featuredIds.Contains(c.Id)).Take(60).Select(CardDto),
                totalCards = cards.Count
            });
        }

        /// <summary>La mia vetrina (o una proposta di partenza) e se il piano la include.</summary>
        [HttpGet("api/me/showcase")]
        public async Task<IActionResult> Mine()
        {
            var showcase = await _context.SellerShowcases.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == Me);
            var user = await _context.Users.AsNoTracking().FirstAsync(u => u.Id == Me);
            var hasPlan = await this.Entitlements(_context).HasAsync(Me, Capability.SellerShowcase);
            var cards = await _context.Cards.AsNoTracking().Include(c => c.CardInfo).ThenInclude(ci => ci.CardSet)
                .Where(c => c.UserId == Me && c.IsAvailableForTrade)
                .OrderByDescending(c => c.CreatedAt).Take(200).ToListAsync();
            return Ok(new
            {
                hasPlan,
                exists = showcase != null,
                slug = showcase?.Slug ?? SuggestSlug(user.Username),
                displayName = showcase?.DisplayName ?? user.Username,
                bio = showcase?.Bio,
                coverImageUrl = showcase?.CoverImageUrl,
                featuredCardIds = ParseIds(showcase?.FeaturedCardIds),
                cards = cards.Select(CardDto)
            });
        }

        public static string SuggestSlug(string username)
        {
            var s = Regex.Replace(username.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            if (s.Length < 3) s = $"banco-{s}".Trim('-');
            return s.Length > 40 ? s[..40].Trim('-') : s;
        }

        [HttpPut("api/me/showcase")]
        [RequireCapability(Capability.SellerShowcase)]
        public async Task<IActionResult> Save([FromBody] ShowcaseRequest request)
        {
            var slug = (request.Slug ?? "").Trim().ToLowerInvariant();
            if (!SlugPattern.IsMatch(slug) || ReservedSlugs.Contains(slug))
                return BadRequest(new { message = "Indirizzo non valido: da 3 a 40 caratteri, solo lettere minuscole, numeri e trattini" });
            var name = (request.DisplayName ?? "").Trim();
            if (name.Length is < 2 or > 60) return BadRequest(new { message = "Il nome della vetrina deve avere da 2 a 60 caratteri" });
            var bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim();
            if (bio?.Length > 500) return BadRequest(new { message = "La descrizione può avere al massimo 500 caratteri" });
            // Stesse regole delle promozioni: niente link, contatti o pagamenti fuori dall'app
            if (PromotionRules.Review(name, bio) is { } reason) return BadRequest(new { message = reason });
            var cover = string.IsNullOrWhiteSpace(request.CoverImageUrl) ? null : request.CoverImageUrl.Trim();
            if (cover != null && (cover.Length > 300 || !Uri.TryCreate(cover, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps))
                return BadRequest(new { message = "L'immagine di copertina deve essere un indirizzo https" });
            if (await _context.SellerShowcases.AnyAsync(s => s.Slug == slug && s.UserId != Me))
                return Conflict(new { message = "Questo indirizzo è già usato da un'altra vetrina" });

            var ids = (request.FeaturedCardIds ?? new()).Distinct().Take(MaxFeatured).ToList();
            var owned = await _context.Cards.Where(c => c.UserId == Me && ids.Contains(c.Id)).Select(c => c.Id).ToListAsync();
            ids = ids.Where(owned.Contains).ToList();

            var showcase = await _context.SellerShowcases.FirstOrDefaultAsync(s => s.UserId == Me);
            if (showcase == null)
            {
                showcase = new SellerShowcase { UserId = Me };
                _context.SellerShowcases.Add(showcase);
            }
            showcase.Slug = slug;
            showcase.DisplayName = name;
            showcase.Bio = bio;
            showcase.CoverImageUrl = cover;
            showcase.FeaturedCardIds = ids.Count == 0 ? null : string.Join(',', ids);
            showcase.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Vetrina salvata", slug });
        }

        /// <summary>Statistiche degli ultimi 7 giorni: visite, carte salvate, contatti in chat, persone raggiunte dagli avvisi.</summary>
        [HttpGet("api/me/showcase/stats")]
        [RequireCapability(Capability.SellerShowcase)]
        public async Task<IActionResult> Stats()
        {
            var since = DateTime.UtcNow.AddDays(-7);
            var sinceDay = DateOnly.FromDateTime(since);
            var showcaseId = await _context.SellerShowcases.Where(s => s.UserId == Me).Select(s => (int?)s.Id).FirstOrDefaultAsync();
            var visits = showcaseId == null ? 0 : await _context.ShowcaseVisits.Where(v => v.ShowcaseId == showcaseId && v.Day >= sinceDay).Select(v => v.ViewerId).Distinct().CountAsync();
            var saved = await _context.FavoriteCards.Where(f => f.Card.UserId == Me && f.CreatedAt >= since).Select(f => f.UserId).Distinct().CountAsync();
            var contacts = await _context.Conversations.Where(c => (c.User1Id == Me || c.User2Id == Me) && c.CreatedAt >= since).CountAsync();
            var broadcasts = await _context.LocationBroadcasts.Where(b => b.AuthorId == Me && b.CreatedAt >= since).ToListAsync();
            var sponsoredReach = await _context.Promotions.Where(p => p.OwnerUserId == Me && p.CreatedAt >= since).SumAsync(p => (int?)p.Reach) ?? 0;
            static int Safe(int n) => n >= MinShown ? n : 0;
            return Ok(new
            {
                visits = Safe(visits),
                savedCards = Safe(saved),
                contacts = Safe(contacts),
                broadcasts = broadcasts.Count,
                reached = Safe(broadcasts.Sum(b => b.RecipientCount) + sponsoredReach),
                followers = await _context.Follows.CountAsync(f => f.FollowedUserId == Me)
            });
        }
    }
}
