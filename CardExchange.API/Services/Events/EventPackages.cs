using System.Security.Cryptography;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services.Events
{
    /// <summary>
    /// Pacchetto per organizzatori: attivazione (da acquisto o a mano), espositori invitati con un link,
    /// "Sono qui" degli espositori senza limite giornaliero e con lo stand già compilato.
    /// </summary>
    public static class EventPackages
    {
        /// <summary>Raggio dell'evento in evidenza incluso: Plus 50 km, Regione 100 km.</summary>
        public static int? FeaturedRadiusKm(EventPackageTier tier) => tier switch
        {
            EventPackageTier.Plus => 50,
            EventPackageTier.Region => 100,
            _ => null
        };

        public static bool HasFloorPlan(EventPackageTier tier) => tier >= EventPackageTier.Plus;

        public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();

        /// <summary>I "Sono qui" degli espositori valgono il giorno dell'evento: da 12 ore prima a 12 ore dopo la fine.</summary>
        public static bool InExhibitorWindow(Event ev, DateTime nowUtc) =>
            nowUtc >= ev.StartDate.AddHours(-12) && nowUtc <= (ev.EndDate ?? ev.StartDate.AddHours(10)).AddHours(12);

        /// <summary>Attiva (o migliora) il pacchetto; con Plus e Regione crea l'evento in evidenza già pagato. Idempotente.</summary>
        public static async Task<EventPackage?> ActivateAsync(ApplicationDbContext db, int eventId, EventPackageTier tier, int? purchaseId, int? adminId, DateTime nowUtc)
        {
            var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId);
            if (ev == null) return null;
            var package = await db.EventPackages.FirstOrDefaultAsync(p => p.EventId == eventId);
            if (package == null)
            {
                package = new EventPackage { EventId = eventId, Tier = tier, ActivatedAt = nowUtc };
                db.EventPackages.Add(package);
            }
            else if (tier > package.Tier) package.Tier = tier;
            package.PurchaseId ??= purchaseId;
            package.ActivatedById ??= adminId;

            var radius = FeaturedRadiusKm(package.Tier);
            if (radius != null && ev.Latitude != null && ev.Longitude != null && ev.StartDate > nowUtc)
            {
                var promo = package.PromotionId == null ? null : await db.Promotions.FirstOrDefaultAsync(p => p.Id == package.PromotionId);
                if (promo == null)
                {
                    promo = new Promotion
                    {
                        OwnerUserId = ev.OrganizerId, StoreId = ev.StoreId, Type = PromotionType.FeaturedEvent, TargetId = ev.Id,
                        GameId = ev.GameId, Latitude = ev.Latitude.Value, Longitude = ev.Longitude.Value,
                        StartsAt = nowUtc, EndsAt = new[] { ev.StartDate, nowUtc.AddDays(PromotionRules.MaxFeaturedDays) }.Min(),
                        Status = PromotionStatus.Active, PaidWith = PromotionPayment.Purchase, PurchaseId = purchaseId, CreatedAt = nowUtc
                    };
                    db.Promotions.Add(promo);
                }
                // Il raggio del pacchetto Regione supera quello delle promozioni normali
                promo.RadiusKm = radius.Value;
                await db.SaveChangesAsync();
                package.PromotionId = promo.Id;
            }
            await db.SaveChangesAsync();
            return package;
        }

        /// <summary>
        /// L'espositore accettato di un evento con pacchetto attivo, se l'avviso è pubblicato il giorno dell'evento
        /// (a nome proprio o del proprio negozio). Null altrimenti.
        /// </summary>
        public static async Task<EventExhibitor?> ExhibitorForBroadcastAsync(ApplicationDbContext db, Event ev, int authorId, int? storeId, DateTime nowUtc)
        {
            if (!InExhibitorWindow(ev, nowUtc)) return null;
            if (!await db.EventPackages.AnyAsync(p => p.EventId == ev.Id)) return null;
            return await db.EventExhibitors.AsNoTracking()
                .Where(x => x.EventId == ev.Id && x.AcceptedAt != null && (x.UserId == authorId || (storeId != null && x.StoreId == storeId)))
                .FirstOrDefaultAsync();
        }
    }
}
