using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardExchange.Core.Entities
{
    public class FavoriteCard : BaseEntity
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        public int CardId { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [ForeignKey("CardId")]
        public virtual Card Card { get; set; } = null!;
    }
}
