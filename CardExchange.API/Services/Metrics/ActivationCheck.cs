using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services.Metrics
{
    /// <summary>
    /// "Match entro 48 ore": a 48 ore dall'iscrizione si guarda se il nuovo utente ha almeno un match vicino
    /// e si salva l'esito (User.MatchWithin48h). Misura la densità di una città.
    /// </summary>
    public class ActivationCheck
    {
        public static readonly TimeSpan After = TimeSpan.FromHours(48);
        /// <summary>Oltre questo ritardo l'esito non sarebbe più "entro 48 ore": l'utente resta escluso dalla metrica.</summary>
        public static readonly TimeSpan GiveUpAfter = TimeSpan.FromDays(4);

        private readonly ApplicationDbContext _db;
        private readonly IMatchmakingService _matchmaking;

        public ActivationCheck(ApplicationDbContext db, IMatchmakingService matchmaking)
        {
            _db = db;
            _matchmaking = matchmaking;
        }

        public async Task<int> RunAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var due = nowUtc - After;
            var oldest = nowUtc - GiveUpAfter;
            var users = await _db.Users
                .Where(u => u.MatchWithin48h == null && u.CreatedAt <= due && u.CreatedAt > oldest)
                .OrderBy(u => u.CreatedAt)
                .Take(200)
                .ToListAsync(ct);
            foreach (var user in users)
            {
                var hasWishlistOrCards = await _db.WishlistItems.AnyAsync(w => w.UserId == user.Id, ct)
                                         || await _db.Cards.AnyAsync(c => c.UserId == user.Id && c.IsAvailableForTrade, ct);
                user.MatchWithin48h = hasWishlistOrCards && (await _matchmaking.FindMatchesAsync(user.Id)).Any();
            }
            if (users.Count > 0) await _db.SaveChangesAsync(ct);
            return users.Count;
        }
    }
}
