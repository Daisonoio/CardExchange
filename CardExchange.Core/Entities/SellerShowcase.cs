namespace CardExchange.Core.Entities
{
    /// <summary>Vetrina pubblica di chi vende a mercatini e fiere (piano Banco).</summary>
    public class SellerShowcase : BaseEntity
    {
        public int UserId { get; set; }
        /// <summary>Indirizzo della vetrina: /v/{slug}.</summary>
        public string Slug { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public string? CoverImageUrl { get; set; }
        /// <summary>Fino a 12 carte in evidenza (id separati da virgola, nell'ordine scelto).</summary>
        public string? FeaturedCardIds { get; set; }

        public virtual User User { get; set; } = null!;
    }

    /// <summary>Visita alla vetrina: una per persona al giorno, per le statistiche (mai mostrata con l'identità).</summary>
    public class ShowcaseVisit
    {
        public long Id { get; set; }
        public int ShowcaseId { get; set; }
        public int ViewerId { get; set; }
        public DateOnly Day { get; set; }
    }
}
