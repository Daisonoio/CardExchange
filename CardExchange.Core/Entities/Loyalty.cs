namespace CardExchange.Core.Entities
{
    /// <summary>Programma fedeltà di un negozio: dopo N timbri un premio deciso e pagato dal negozio.</summary>
    public class LoyaltyProgram : BaseEntity
    {
        public int StoreId { get; set; }
        public int StampsRequired { get; set; } = 5;
        /// <summary>Premio, es. "Una bustina in omaggio".</summary>
        public string RewardDescription { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;

        public virtual Store Store { get; set; } = null!;
    }

    /// <summary>Tessera di un utente presso un negozio.</summary>
    public class LoyaltyCard : BaseEntity
    {
        public int StoreId { get; set; }
        public int UserId { get; set; }
        /// <summary>Timbri non ancora usati per un premio.</summary>
        public int Stamps { get; set; }
        public int RewardsRedeemed { get; set; }
        public DateTime? LastStampAt { get; set; }
        public DateTime? LastRedeemedAt { get; set; }

        public virtual Store Store { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }

    public enum LoyaltyStampSource
    {
        EventCheckIn = 1,
        StaffScan = 2
    }

    /// <summary>Un timbro: al massimo uno per tessera e giorno (indice univoco su tessera e giorno).</summary>
    public class LoyaltyStamp : BaseEntity
    {
        public int LoyaltyCardId { get; set; }
        public LoyaltyStampSource Source { get; set; }
        /// <summary>Evento del check-in, se il timbro viene da lì.</summary>
        public int? RefId { get; set; }
        /// <summary>Giorno del timbro in ora italiana.</summary>
        public DateOnly Day { get; set; }

        public virtual LoyaltyCard LoyaltyCard { get; set; } = null!;
    }
}
