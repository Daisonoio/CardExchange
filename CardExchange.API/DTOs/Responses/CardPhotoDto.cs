namespace CardExchange.API.DTOs.Responses
{
    public class CardPhotoDto
    {
        public int Id { get; set; }
        public int CardId { get; set; }
        public int UploadedByUserId { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public int FileSizeBytes { get; set; }
        public DateTime CreatedAt { get; set; }
        public string DownloadUrl { get; set; } = string.Empty;
    }
}
