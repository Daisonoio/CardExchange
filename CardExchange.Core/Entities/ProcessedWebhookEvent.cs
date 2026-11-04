using System.ComponentModel.DataAnnotations;

namespace CardExchange.Core.Entities
{
    /// <summary>Eventi webhook già elaborati: i provider possono reinviare lo stesso evento più volte.</summary>
    public class ProcessedWebhookEvent : BaseEntity
    {
        [Required, MaxLength(30)]
        public string Provider { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string EventId { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? EventType { get; set; }
    }
}
