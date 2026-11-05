using System.ComponentModel.DataAnnotations;

namespace CardExchange.Core.Entities
{
    public enum ErrorSource
    {
        Server = 1,
        /// <summary>Errore nel browser dell'utente (app web).</summary>
        Client = 2
    }

    /// <summary>
    /// Gruppo di errori uguali (stessa "impronta"): si conta quante volte capita invece di salvare ogni occorrenza.
    /// Gli amministratori ricevono un avviso alla prima comparsa e quando un errore risolto si ripresenta.
    /// </summary>
    public class ErrorGroup : BaseEntity
    {
        [MaxLength(64)]
        public string Fingerprint { get; set; } = string.Empty;
        public ErrorSource Source { get; set; }
        [MaxLength(200)]
        public string Category { get; set; } = string.Empty;
        [MaxLength(500)]
        public string Message { get; set; } = string.Empty;
        [MaxLength(4000)]
        public string? Detail { get; set; }
        [MaxLength(300)]
        public string? Path { get; set; }
        public int Count { get; set; }
        public DateTime FirstSeenAt { get; set; }
        public DateTime LastSeenAt { get; set; }
        public bool IsResolved { get; set; }
        public DateTime? NotifiedAt { get; set; }
    }

    /// <summary>Stato dei cataloghi esterni delle carte, controllato periodicamente.</summary>
    public class CatalogStatus : BaseEntity
    {
        [MaxLength(40)]
        public string Key { get; set; } = string.Empty;
        public DateTime? LastCheckAt { get; set; }
        public DateTime? LastOkAt { get; set; }
        public DateTime? LastFailAt { get; set; }
        public int ConsecutiveFailures { get; set; }
        [MaxLength(500)]
        public string? LastError { get; set; }
        /// <summary>Avviso di guasto già inviato per l'interruzione in corso.</summary>
        public bool OutageAlerted { get; set; }
    }
}
