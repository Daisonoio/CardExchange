using System.ComponentModel.DataAnnotations.Schema;

namespace CardExchange.Core.Entities
{
    /// <summary>
    /// Un utente segue un altro utente oppure un negozio (esattamente uno dei due).
    /// Chi segue riceve gli avvisi "Sono qui" del profilo seguito. Smettere di seguire = cancellazione fisica.
    /// </summary>
    public class Follow : BaseEntity
    {
        public int FollowerId { get; set; }

        public int? FollowedUserId { get; set; }

        public int? FollowedStoreId { get; set; }

        [ForeignKey(nameof(FollowerId))]
        public virtual User Follower { get; set; } = null!;

        [ForeignKey(nameof(FollowedUserId))]
        public virtual User? FollowedUser { get; set; }

        [ForeignKey(nameof(FollowedStoreId))]
        public virtual Store? FollowedStore { get; set; }
    }
}
