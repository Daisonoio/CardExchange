using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardExchange.Core.Entities
{
    public enum CardCondition
    {
        Mint = 1,
        NearMint = 2,
        Excellent = 3,
        Good = 4,
        LightlyPlayed = 5,
        ModeratelyPlayed = 6,
        HeavilyPlayed = 7,
        Damaged = 8
    }

    /// <summary>Finitura della copia fisica. Reverse Holo è tipica di Pokémon, Etched di Magic.</summary>
    public enum CardFinish
    {
        Normal = 1,
        Foil = 2,
        ReverseHolo = 3,
        Etched = 4
    }

    public class Card : BaseEntity
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        public int CardInfoId { get; set; }

        [Required]
        public CardCondition Condition { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "La quantità deve essere almeno 1")]
        public int Quantity { get; set; } = 1;

        /// <summary>Codice lingua (vedi <see cref="Constants.CardLanguages"/>). Null = non specificata.</summary>
        [MaxLength(5)]
        public string? Language { get; set; }

        public CardFinish Finish { get; set; } = CardFinish.Normal;

        [MaxLength(500)]
        public string? Notes { get; set; }

        public bool IsAvailableForTrade { get; set; } = true;

        /// <summary>Copie bloccate da prenotazioni in negozio attive ("Prenota e ritira"): disponibili = Quantity − ReservedQuantity.</summary>
        public int ReservedQuantity { get; set; }

        /// <summary>Valore stimato registrato all'inserimento (es. prezzo di mercato all'import). Usato solo se il catalogo non ha prezzi.</summary>
        public decimal? EstimatedValue { get; set; }

        /// <summary>
        /// Prezzo indicato dal proprietario: la cifra a cui sarebbe disposto a cedere la carta. Indicativo e trattabile;
        /// se assente si usa il valore di mercato. La piattaforma non gestisce pagamenti né conclude vendite.
        /// </summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal? AskingPrice { get; set; }

        // Relazioni
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [ForeignKey("CardInfoId")]
        public virtual CardInfo CardInfo { get; set; } = null!;

        public virtual ICollection<CardPhoto> Photos { get; set; } = new List<CardPhoto>();
    }
}