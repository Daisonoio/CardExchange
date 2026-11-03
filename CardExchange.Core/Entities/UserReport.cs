using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardExchange.Core.Entities
{
    public enum ReportReason
    {
        Scam = 1,
        NoShow = 2,
        Harassment = 3,
        CardsNotAsDescribed = 4,
        Spam = 5,
        Other = 6
    }

    public enum ReportStatus
    {
        Open = 1,
        Resolved = 2,
        Dismissed = 3
    }

    /// <summary>Segnalazione di un utente, gestita dai moderatori (permesso ADMIN.REPORTS.MANAGE).</summary>
    public class UserReport : BaseEntity
    {
        [Required]
        public int ReporterId { get; set; }

        [Required]
        public int ReportedUserId { get; set; }

        [Required]
        public ReportReason Reason { get; set; }

        [MaxLength(1000)]
        public string? Details { get; set; }

        /// <summary>Scambio a cui si riferisce la segnalazione, se presente.</summary>
        public int? TradeOfferId { get; set; }

        public ReportStatus Status { get; set; } = ReportStatus.Open;

        public int? ResolvedById { get; set; }
        public DateTime? ResolvedAt { get; set; }

        [MaxLength(1000)]
        public string? ResolutionNote { get; set; }

        /// <summary>True se la risoluzione ha sospeso l'account segnalato.</summary>
        public bool UserSuspended { get; set; }

        [ForeignKey(nameof(ReporterId))]
        public virtual User Reporter { get; set; } = null!;

        [ForeignKey(nameof(ReportedUserId))]
        public virtual User ReportedUser { get; set; } = null!;
    }
}
