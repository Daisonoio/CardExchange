using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardExchange.Core.Entities
{
    public enum AlertDirection
    {
        Above = 1,   // Notifica quando il prezzo sale sopra la soglia
        Below = 2    // Notifica quando il prezzo scende sotto la soglia
    }

    public enum AlertCurrency
    {
        Usd = 1,
        Eur = 2
    }

    public class PriceAlert : BaseEntity
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        public int CardInfoId { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal TargetPrice { get; set; }

        public AlertDirection Direction { get; set; } = AlertDirection.Below;
        public AlertCurrency Currency { get; set; } = AlertCurrency.Eur;

        public bool IsActive { get; set; } = true;
        public bool IsTriggered { get; set; } = false;
        public DateTime? TriggeredAt { get; set; }

        [MaxLength(200)]
        public string? Notes { get; set; }

        // Relazioni
        public virtual User User { get; set; } = null!;
        public virtual CardInfo CardInfo { get; set; } = null!;
    }
}
