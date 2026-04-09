using System.ComponentModel.DataAnnotations;

namespace CardExchange.Core.Entities
{
    public class AppConfigKey : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string ServiceName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string KeyName { get; set; } = string.Empty;

        [Required]
        public string KeyValue { get; set; } = string.Empty;

        public bool IsEncrypted { get; set; } = false;

        public DateTime? ExpiresAt { get; set; }

        public bool IsActive { get; set; } = true;

        [MaxLength(300)]
        public string? Description { get; set; }
    }
}
