using System.ComponentModel.DataAnnotations;

namespace CardExchange.Core.Entities
{
    public class AppConfig : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        [Required]
        public string Value { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Category { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        [MaxLength(20)]
        public string ValueType { get; set; } = "string"; // string, int, bool, decimal, json

        public bool IsActive { get; set; } = true;
    }
}
