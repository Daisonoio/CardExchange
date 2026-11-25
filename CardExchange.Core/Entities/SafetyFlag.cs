namespace CardExchange.Core.Entities
{
    public enum SafetyFlagKind
    {
        /// <summary>Frasi di adescamento da un adulto verso un minorenne.</summary>
        Grooming = 1,
        /// <summary>Droghe, armi, contenuti sessuali.</summary>
        Illegal = 2,
        /// <summary>Un adulto ha provato a dare o chiedere contatti a un minorenne.</summary>
        ContactToMinor = 3,
        /// <summary>Un adulto contatta molti minorenni diversi.</summary>
        ManyMinors = 4,
        /// <summary>Richiesta di denaro online in una conversazione con un minorenne.</summary>
        PaymentWithMinor = 5,
        /// <summary>Segnalazione di un utente per comportamento verso un minore o proposte illegali.</summary>
        UserReport = 6
    }

    public enum SafetyFlagStatus { Open = 1, Dismissed = 2, ActionTaken = 3 }

    /// <summary>
    /// Segnalazione automatica di sicurezza (o da utente) per i moderatori. Conserva solo un estratto del messaggio,
    /// che non viene recapitato quando è bloccato.
    /// </summary>
    public class SafetyFlag : BaseEntity
    {
        public int SenderId { get; set; }
        public int? RecipientId { get; set; }
        public int? ConversationId { get; set; }
        public SafetyFlagKind Kind { get; set; }
        /// <summary>Il messaggio è stato bloccato (non recapitato).</summary>
        public bool Blocked { get; set; }
        /// <summary>Estratto del testo (massimo 300 caratteri), per la verifica.</summary>
        public string? Excerpt { get; set; }
        /// <summary>Il destinatario è minorenne (o senza età indicata).</summary>
        public bool InvolvesMinor { get; set; }
        public SafetyFlagStatus Status { get; set; } = SafetyFlagStatus.Open;
        public int? ReviewedById { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNote { get; set; }

        public virtual User Sender { get; set; } = null!;
    }
}
