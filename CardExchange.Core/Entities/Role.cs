using System.ComponentModel.DataAnnotations;

namespace CardExchange.Core.Entities
{
    public class Role : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsSystemRole { get; set; } = false; // I ruoli di sistema non possono essere eliminati

        // Relazioni
        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}