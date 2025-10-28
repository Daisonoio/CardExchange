using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardExchange.Core.Entities
{
    public class UserRole : BaseEntity
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        public int RoleId { get; set; }

        public int? AssignedBy { get; set; } // Chi ha assegnato il ruolo

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        // Relazioni
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [ForeignKey("RoleId")]
        public virtual Role Role { get; set; } = null!;

        [ForeignKey("AssignedBy")]
        public virtual User? AssignedByUser { get; set; }
    }
}