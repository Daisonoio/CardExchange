using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardExchange.Core.Entities
{
    /// <summary>
    /// Blocco tra utenti. Ha effetto in entrambe le direzioni: i due utenti non si vedono più in
    /// matchmaking ed esplorazione, non possono scriversi né proporsi scambi. Sblocco = cancellazione fisica.
    /// </summary>
    public class UserBlock : BaseEntity
    {
        [Required]
        public int BlockerId { get; set; }

        [Required]
        public int BlockedUserId { get; set; }

        [ForeignKey(nameof(BlockerId))]
        public virtual User Blocker { get; set; } = null!;

        [ForeignKey(nameof(BlockedUserId))]
        public virtual User BlockedUser { get; set; } = null!;
    }
}
