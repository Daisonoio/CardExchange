using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardExchange.Core.Entities
{
    public class PriceHistory : BaseEntity
    {
        [Required]
        public int CardInfoId { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? PriceUsd { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? PriceUsdFoil { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? PriceEur { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? PriceEurFoil { get; set; }

        // Data dello snapshot (normalizzata a mezzanotte UTC)
        [Required]
        public DateTime SnapshotDate { get; set; }

        // Relazioni
        public virtual CardInfo CardInfo { get; set; } = null!;
    }
}
