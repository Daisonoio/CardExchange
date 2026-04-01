using System.ComponentModel.DataAnnotations;

namespace CardExchange.API.DTOs.Requests
{
    public class UploadCardPhotoRequest
    {
        [Required]
        [MaxLength(1_000_000)]
        public string Base64Image { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string ContentType { get; set; } = "image/webp";
    }
}
