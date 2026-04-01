using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardExchange.Core.Entities
{
    public class CardPhoto : BaseEntity
    {
        [Required]
        public int CardId { get; set; }

        [Required]
        public int UploadedByUserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string ContentType { get; set; } = "image/webp";

        [Required]
        public byte[] ImageData { get; set; } = Array.Empty<byte>();

        [Required]
        public int FileSizeBytes { get; set; }

        [ForeignKey("CardId")]
        public virtual Card Card { get; set; } = null!;

        [ForeignKey("UploadedByUserId")]
        public virtual User UploadedByUser { get; set; } = null!;
    }
}
