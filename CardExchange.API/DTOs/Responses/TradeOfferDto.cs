namespace CardExchange.API.DTOs.Responses
{
    public class TradeOfferDto
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public string SenderUsername { get; set; } = string.Empty;
        public int ReceiverId { get; set; }
        public string ReceiverUsername { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Message { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ResponseDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public int? ParentOfferId { get; set; }
        public DateTime? SenderConfirmedAt { get; set; }
        public DateTime? ReceiverConfirmedAt { get; set; }
        public List<TradeOfferItemDto> OfferedCards { get; set; } = new();
        public List<TradeOfferItemDto> RequestedCards { get; set; } = new();
    }

    public class TradeOfferItemDto
    {
        public int Id { get; set; }
        public int CardId { get; set; }
        public string CardName { get; set; } = string.Empty;
        public string CardSetName { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;
        public string? Language { get; set; }
        public string Finish { get; set; } = "Normal";
        public int Quantity { get; set; }
        public string OwnerUsername { get; set; } = string.Empty;
    }

    public class TradeReviewDto
    {
        public int Id { get; set; }
        public int TradeOfferId { get; set; }
        public int ReviewerId { get; set; }
        public string ReviewerUsername { get; set; } = string.Empty;
        public int ReviewedUserId { get; set; }
        public string ReviewedUserUsername { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public bool CardAsDescribed { get; set; }
        public bool TimelyShipping { get; set; }
        public bool GoodCommunication { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class TradeEvaluationDto
    {
        public decimal OfferedTotal { get; set; }
        public decimal RequestedTotal { get; set; }
        /// <summary>Offerto meno richiesto: positivo = chi propone dà di più.</summary>
        public decimal Difference { get; set; }
        public decimal Tolerance { get; set; }
        /// <summary>fair, favorsReceiver, favorsProposer.</summary>
        public string Verdict { get; set; } = "fair";
        /// <summary>Carte senza alcun prezzo (escluse dal totale).</summary>
        public int MissingPriceCount { get; set; }
        public List<TradeItemValueDto> Items { get; set; } = new();
    }

    public class TradeItemValueDto
    {
        public int CardId { get; set; }
        public int Quantity { get; set; }
        public decimal? UnitValue { get; set; }
    }

    public class TradeSuggestionDto
    {
        public List<TradeItemValueDto> OfferedCards { get; set; } = new();
        public TradeEvaluationDto Evaluation { get; set; } = new();
        /// <summary>Quante tue carte disponibili l'altro utente cerca.</summary>
        public int CandidatesCount { get; set; }
        public string? Message { get; set; }
    }
}
