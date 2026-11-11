using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CardExchange.Infrastructure.Data
{
    /// <summary>
    /// Solo per lo sviluppo con SQLite: aggiunge al database esistente tabelle, colonne e indici che il modello
    /// ha in più (EnsureCreated non lo fa se il file esiste già). Non cancella né modifica nulla.
    /// In produzione (SQL Server) lo schema si aggiorna con le migrazioni.
    /// </summary>
    public static class SqliteSchemaSync
    {
        public record Result(IReadOnlyList<string> TablesCreated, IReadOnlyList<string> ColumnsAdded, int IndexesEnsured, IReadOnlyList<string> Warnings);

        public static async Task<Result> ApplyAsync(DbContext context, CancellationToken ct = default)
        {
            var tablesCreated = new List<string>();
            var columnsAdded = new List<string>();
            var warnings = new List<string>();
            var indexes = 0;

            var script = context.Database.GenerateCreateScript();
            var statements = Regex.Split(script, @";\s*\r?\n").Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            var existingTables = await ScalarListAsync(context, "SELECT name FROM sqlite_master WHERE type = 'table'", ct);

            // 1. Tabelle mancanti: si creano con lo stesso comando di EnsureCreated (chiavi esterne comprese)
            foreach (var stmt in statements.Where(s => s.StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase)))
            {
                var name = Regex.Match(stmt, "CREATE TABLE \"([^\"]+)\"").Groups[1].Value;
                if (name.Length == 0 || existingTables.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
                await context.Database.ExecuteSqlRawAsync(stmt, ct);
                tablesCreated.Add(name);
            }

            // 2. Colonne mancanti nelle tabelle già esistenti, lette dal modello
            foreach (var entity in context.Model.GetEntityTypes().Where(e => e.GetTableName() != null && e.GetViewName() == null))
            {
                var table = entity.GetTableName()!;
                if (tablesCreated.Contains(table)) continue;
                var storeObject = StoreObjectIdentifier.Table(table, entity.GetSchema());
                var existingColumns = await ScalarListAsync(context, $"SELECT name FROM pragma_table_info('{table.Replace("'", "''")}')", ct);
                if (existingColumns.Count == 0) continue;

                foreach (var property in entity.GetProperties())
                {
                    var column = property.GetColumnName(storeObject);
                    if (column == null || existingColumns.Contains(column, StringComparer.OrdinalIgnoreCase)) continue;
                    var sql = $"ALTER TABLE \"{table}\" ADD COLUMN {ColumnDefinition(property, column, storeObject)}";
                    try
                    {
                        await context.Database.ExecuteSqlRawAsync(sql, ct);
                        columnsAdded.Add($"{table}.{column}");
                        existingColumns.Add(column);
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"{table}.{column}: {ex.Message}");
                    }
                }
            }

            // Dati da allineare quando arriva una colonna nuova (come nella migrazione corrispondente)
            if (columnsAdded.Contains("Users.ProfileType"))
            {
                // Titolari di negozi verificati → Negozio (2); piano Banco attivo → Venditore (1)
                await context.Database.ExecuteSqlRawAsync("UPDATE Users SET ProfileType = 2 WHERE Id IN (SELECT OwnerId FROM Stores WHERE Status = 2)", ct);
                await context.Database.ExecuteSqlRawAsync(@"UPDATE Users SET ProfileType = 1 WHERE ProfileType = 0 AND Id IN (
                    SELECT s.UserId FROM UserSubscriptions s JOIN SubscriptionPlans p ON p.Id = s.PlanId WHERE s.Status = 1 AND p.Tier = 3)", ct);
            }

            // 3. Indici: tutti con IF NOT EXISTS; un indice univoco può fallire se i dati esistenti hanno doppioni
            foreach (var stmt in statements.Where(s => s.StartsWith("CREATE INDEX", StringComparison.OrdinalIgnoreCase) || s.StartsWith("CREATE UNIQUE INDEX", StringComparison.OrdinalIgnoreCase)))
            {
                var safe = Regex.Replace(stmt, @"^CREATE (UNIQUE )?INDEX ", m => m.Value + "IF NOT EXISTS ", RegexOptions.IgnoreCase);
                try
                {
                    await context.Database.ExecuteSqlRawAsync(safe, ct);
                    indexes++;
                }
                catch (Exception ex)
                {
                    warnings.Add($"{Regex.Match(stmt, "INDEX \"([^\"]+)\"").Groups[1].Value}: {ex.Message}");
                }
            }

            return new Result(tablesCreated, columnsAdded, indexes, warnings);
        }

        /// <summary>Definizione di colonna per ALTER TABLE: le NOT NULL ricevono un valore predefinito per le righe esistenti.</summary>
        private static string ColumnDefinition(IProperty property, string column, StoreObjectIdentifier storeObject)
        {
            var type = property.GetColumnType(storeObject) ?? "TEXT";
            var nullable = property.IsColumnNullable(storeObject);
            if (nullable) return $"\"{column}\" {type} NULL";
            return $"\"{column}\" {type} NOT NULL DEFAULT {DefaultFor(property.ClrType)}";
        }

        private static string DefaultFor(Type clr)
        {
            var t = Nullable.GetUnderlyingType(clr) ?? clr;
            if (t == typeof(string)) return "''";
            if (t == typeof(bool) || t.IsEnum || t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)) return "0";
            if (t == typeof(decimal) || t == typeof(double) || t == typeof(float)) return "0";
            if (t == typeof(DateTime) || t == typeof(DateTimeOffset)) return "'0001-01-01 00:00:00'";
            if (t == typeof(DateOnly)) return "'0001-01-01'";
            if (t == typeof(TimeOnly) || t == typeof(TimeSpan)) return "'00:00:00'";
            if (t == typeof(Guid)) return "'00000000-0000-0000-0000-000000000000'";
            if (t == typeof(byte[])) return "X''";
            return "''";
        }

        private static async Task<List<string>> ScalarListAsync(DbContext context, string sql, CancellationToken ct)
        {
            var list = new List<string>();
            var conn = context.Database.GetDbConnection();
            var wasOpen = conn.State == System.Data.ConnectionState.Open;
            if (!wasOpen) await conn.OpenAsync(ct);
            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct)) list.Add(reader.GetString(0));
            }
            finally
            {
                if (!wasOpen) await conn.CloseAsync();
            }
            return list;
        }
    }
}
