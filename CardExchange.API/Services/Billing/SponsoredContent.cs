using CardExchange.API.Services.Entitlements;
using CardExchange.API.Services.Moderation;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services.Billing
{
    public record SlotWhy(string Zone, string? City, int? RadiusKm, string? Game, bool Brand);

    public record SlotView(SponsoredSlot Slot, string SponsorName, SlotWhy Why);

    public record EventSponsor(int SponsorId, string Name, string? LogoUrl, SponsorshipPlacement Placement);

    /// <summary>
    /// Spazi sponsorizzati e sponsor di marca: scelta dello spazio da mostrare (zona, gioco, età, limite di frequenza)
    /// e sponsor delle pagine evento e delle serate di scambio.
    /// </summary>
    public class SponsoredContent
    {
        private readonly ApplicationDbContext _db;
        private readonly IUserBlockService _blocks;
        private readonly IEntitlementService _entitlements;

        public SponsoredContent(ApplicationDbContext db, IUserBlockService blocks, IEntitlementService? entitlements = null)
        {
            _db = db;
            _blocks = blocks;
            _entitlements = entitlements.OrDefault(db);
        }

        /// <summary>
        /// Lo spazio da mostrare in un punto dell'app, o null. Conta la visualizzazione (una per persona e giorno nelle statistiche).
        /// Minorenni ed età non indicata: solo spazi senza zona, scelti in base alla pagina e al gioco attivo.
        /// Premium: niente sponsor di marca, sì promozioni locali.
        /// </summary>
        public async Task<SlotView?> PickAsync(int userId, SlotPlacement placement, decimal? latitude, decimal? longitude, int? gameId, DateTime nowUtc)
        {
            var user = await _db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new { u.BirthDate }).FirstOrDefaultAsync();
            if (user == null) return null;
            var today = DateOnly.FromDateTime(TimeZones.ToLocal(nowUtc, null));
            var adult = user.BirthDate is { } bd && !AgeRules.IsMinor(bd, DateOnly.FromDateTime(nowUtc));
            var hideBrands = await _entitlements.HasAsync(userId, Capability.HidesBrandSponsors);

            if (adult && (latitude == null || longitude == null))
            {
                var loc = await _db.UserLocations.AsNoTracking().FirstOrDefaultAsync(l => l.UserId == userId);
                latitude = loc?.Latitude; longitude = loc?.Longitude;
            }

            var candidates = await _db.SponsoredSlots.AsNoTracking()
                .Where(s => s.IsActive && s.Placement == placement && s.StartsAt <= nowUtc && s.EndsAt > nowUtc
                            && (s.GameId == null || s.GameId == gameId))
                .ToListAsync();
            if (hideBrands) candidates = candidates.Where(s => s.SponsorshipId == null).ToList();
            if (!adult) candidates = candidates.Where(s => s.Latitude == null).ToList();
            candidates = candidates.Where(s => s.Latitude == null || (latitude != null && longitude != null
                && Geo.DistanceKm((double)latitude.Value, (double)longitude.Value, (double)s.Latitude, (double)s.Longitude!.Value) <= (s.RadiusKm ?? 0))).ToList();
            if (candidates.Count == 0) return null;

            var blocked = await _blocks.GetBlockedUserIdsAsync(userId);
            candidates = candidates.Where(s => s.OwnerUserId == null || (s.OwnerUserId != userId && !blocked.Contains(s.OwnerUserId.Value))).ToList();
            var ids = candidates.Select(s => s.Id).ToList();
            var seen = await _db.SlotImpressions.AsNoTracking().Where(i => i.UserId == userId && i.Day == today && ids.Contains(i.SlotId))
                .ToDictionaryAsync(i => i.SlotId, i => i.Count);
            // Limite di frequenza per persona e giorno; tra i rimasti quello visto meno oggi, a parità a caso
            var slot = candidates.Where(s => seen.GetValueOrDefault(s.Id) < s.FrequencyCapPerDay)
                .OrderBy(s => seen.GetValueOrDefault(s.Id)).ThenBy(_ => Random.Shared.Next()).FirstOrDefault();
            if (slot == null) return null;

            // Conteggio sul server: la prima vista del giorno conta come impressione
            var first = !seen.ContainsKey(slot.Id);
            if (first)
            {
                _db.SlotImpressions.Add(new SlotImpression { SlotId = slot.Id, UserId = userId, Day = today, Count = 1 });
                try
                {
                    await _db.SaveChangesAsync();
                    await _db.SponsoredSlots.Where(s => s.Id == slot.Id).ExecuteUpdateAsync(u => u.SetProperty(s => s.Impressions, s => s.Impressions + 1));
                }
                catch (DbUpdateException) { _db.ChangeTracker.Clear(); }
            }
            else
            {
                await _db.SlotImpressions.Where(i => i.SlotId == slot.Id && i.UserId == userId && i.Day == today)
                    .ExecuteUpdateAsync(u => u.SetProperty(i => i.Count, i => i.Count + 1));
            }

            string sponsorName;
            if (slot.SponsorshipId != null)
                sponsorName = await _db.Sponsorships.AsNoTracking().Where(x => x.Id == slot.SponsorshipId).Select(x => x.Sponsor.Name).FirstOrDefaultAsync() ?? "";
            else
            {
                var storeName = await _db.Promotions.AsNoTracking().Where(p => p.Id == slot.PromotionId && p.StoreId != null)
                    .Join(_db.Stores, p => p.StoreId, st => st.Id, (p, st) => st.Name).FirstOrDefaultAsync();
                sponsorName = storeName ?? await _db.Users.AsNoTracking().Where(u => u.Id == slot.OwnerUserId).Select(u => u.Username).FirstOrDefaultAsync() ?? "";
            }
            var game = slot.GameId == null ? null : await _db.Games.AsNoTracking().Where(g => g.Id == slot.GameId).Select(g => g.Name).FirstOrDefaultAsync();
            var why = new SlotWhy(slot.Latitude == null ? "Italia" : "Zona", slot.City, slot.RadiusKm, game, slot.SponsorshipId != null);
            return new SlotView(slot, sponsorName, why);
        }

        public Task ClickAsync(int slotId) =>
            _db.SponsoredSlots.Where(s => s.Id == slotId).ExecuteUpdateAsync(u => u.SetProperty(s => s.Clicks, s => s.Clicks + 1));

        /// <summary>Sponsorizzazione attiva che copre l'evento: evento singolo, serate di scambio della città o periodo.</summary>
        public static bool Covers(Sponsorship s, Event ev, DateTime nowUtc)
        {
            if (nowUtc < s.StartsAt || nowUtc >= s.EndsAt) return false;
            var city = (s.ScopeRef ?? "").Trim();
            var sameCity = city.Length == 0 || string.Equals(city, ev.City?.Trim(), StringComparison.OrdinalIgnoreCase);
            var inPeriod = ev.StartDate >= s.StartsAt && ev.StartDate < s.EndsAt;
            return s.Scope switch
            {
                SponsorshipScope.Event => city == ev.Id.ToString(),
                SponsorshipScope.CityTradeNights => ev.Type == EventType.Trademeetup && city.Length > 0 && sameCity && inPeriod,
                SponsorshipScope.Month => sameCity && inPeriod,
                _ => false
            };
        }

        /// <summary>Sponsor da mostrare sull'evento ("Offerto da …"). Visibile a tutti, anche ai Premium.</summary>
        public async Task<List<EventSponsor>> ForEventAsync(Event ev, DateTime nowUtc)
        {
            var active = await _db.Sponsorships.AsNoTracking().Include(s => s.Sponsor)
                .Where(s => s.Placement != SponsorshipPlacement.Slot && s.StartsAt <= nowUtc && s.EndsAt > nowUtc && s.Sponsor.IsActive)
                .ToListAsync();
            return active.Where(s => Covers(s, ev, nowUtc))
                .Select(s => new EventSponsor(s.SponsorId, s.Sponsor.Name, s.Sponsor.LogoUrl, s.Placement))
                .GroupBy(s => (s.SponsorId, s.Placement)).Select(g => g.First()).ToList();
        }

        /// <summary>Una sponsorizzazione di tipo spazio crea (o aggiorna) lo spazio di marca corrispondente.</summary>
        public async Task SyncBrandSlotAsync(Sponsorship s)
        {
            var slot = await _db.SponsoredSlots.FirstOrDefaultAsync(x => x.SponsorshipId == s.Id);
            if (s.Placement != SponsorshipPlacement.Slot)
            {
                if (slot != null) slot.IsActive = false;
                await _db.SaveChangesAsync();
                return;
            }
            if (slot == null)
            {
                slot = new SponsoredSlot { SponsorshipId = s.Id };
                _db.SponsoredSlots.Add(slot);
            }
            slot.Placement = s.SlotPlacement ?? SlotPlacement.HomeFeed;
            slot.Title = s.SlotTitle ?? "";
            slot.Body = s.SlotBody ?? "";
            slot.TargetUrl = $"/sponsors/{s.SponsorId}";
            slot.GameId = s.GameId;
            slot.StartsAt = s.StartsAt;
            slot.EndsAt = s.EndsAt;
            slot.IsActive = true;
            slot.ImageUrl = await _db.Sponsors.Where(x => x.Id == s.SponsorId).Select(x => x.LogoUrl).FirstOrDefaultAsync();
            await _db.SaveChangesAsync();
        }
    }
}
