using System.ComponentModel.DataAnnotations;

namespace CardExchange.API.DTOs.Requests
{
    public class CreateSubscriptionRequest
    {
        [Required]
        public int PlanId { get; set; }

        [MaxLength(100)]
        public string? PaymentReference { get; set; }
    }

    public class CancelSubscriptionRequest
    {
        [MaxLength(500)]
        public string? Reason { get; set; }
    }
}
