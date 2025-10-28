using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardExchange.Core.Entities
{
    public class RolePermission
    {
        [Required]
        public int RoleId { get; set; }

        [Required]
        public int PermissionId { get; set; }

        // Relazioni
        [ForeignKey("RoleId")]
        public virtual Role Role { get; set; } = null!;

        [ForeignKey("PermissionId")]
        public virtual Permission Permission { get; set; } = null!;
    }
}