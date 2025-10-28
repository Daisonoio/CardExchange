using System.ComponentModel.DataAnnotations;

namespace CardExchange.Core.Entities
{
    public class Permission : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty; // Es: USERS.READ.ALL

        [Required]
        [MaxLength(50)]
        public string Category { get; set; } = string.Empty; // Es: Users, Cards, Wishlist

        [MaxLength(500)]
        public string? Description { get; set; }

        // Relazioni
        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}