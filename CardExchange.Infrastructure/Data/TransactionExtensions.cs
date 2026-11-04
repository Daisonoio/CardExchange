using Microsoft.EntityFrameworkCore;

namespace CardExchange.Infrastructure.Data
{
    /// <summary>
    /// Transazioni compatibili con la strategia di ripetizione automatica di SQL Server (EnableRetryOnFailure):
    /// EF non accetta BeginTransaction "a mano" con quella strategia, quindi l'intero blocco va eseguito
    /// dentro CreateExecutionStrategy(), che in caso di errore temporaneo lo ripete come un'unica unità.
    /// </summary>
    public static class TransactionExtensions
    {
        /// <summary>
        /// Esegue <paramref name="action"/> in una transazione e la conferma se non ci sono eccezioni
        /// (un'eccezione annulla tutto). Se è già aperta una transazione si usa quella.
        /// In caso di ripetizione si svuota il tracciamento delle entità, così il blocco riparte dai dati
        /// del database: va scritto in modo da ricaricare ciò che modifica.
        /// </summary>
        public static async Task<T> InTransactionAsync<T>(this DbContext context, Func<Task<T>> action, CancellationToken ct = default)
        {
            if (context.Database.CurrentTransaction != null)
                return await action();

            var strategy = context.Database.CreateExecutionStrategy();
            var attempt = 0;
            return await strategy.ExecuteAsync(async () =>
            {
                if (attempt++ > 0) context.ChangeTracker.Clear();
                await using var transaction = await context.Database.BeginTransactionAsync(ct);
                var result = await action();
                await transaction.CommitAsync(ct);
                return result;
            });
        }

        public static Task InTransactionAsync(this DbContext context, Func<Task> action, CancellationToken ct = default) =>
            context.InTransactionAsync(async () => { await action(); return true; }, ct);
    }
}
