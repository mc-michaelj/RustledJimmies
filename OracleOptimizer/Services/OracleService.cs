using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Threading.Tasks;
using OracleOptimizer; // Added for Logger
using System.Diagnostics; // For Stopwatch

namespace OracleOptimizer.Services
{
    public class OracleService
    {
        // Helper to log SQL, truncating if too long
        private string SanitizeSqlForLogging(string sql)
        {
            const int MAX_SQL_LOG_LENGTH = 500; // Log first 500 chars of SQL
            if (sql.Length > MAX_SQL_LOG_LENGTH)
            {
                return sql.Substring(0, MAX_SQL_LOG_LENGTH) + "... (truncated)";
            }
            return sql;
        }

        public async Task<DataTable> ExecuteQueryAsync(string connectionString, string sql)
        {
            var dataTable = new DataTable();
            var stopwatch = new Stopwatch();
            string sanitizedSql = SanitizeSqlForLogging(sql);
            Logger.LogInfo($"ExecuteQueryAsync starting. SQL: {sanitizedSql}");

            try
            {
                using (var connection = new OracleConnection(connectionString))
                {
                    await connection.OpenAsync();
                    Logger.LogInfo("Connection opened for ExecuteQueryAsync.");
                    using (var command = new OracleCommand(sql, connection))
                    {
                        stopwatch.Start();
                        using (var adapter = new OracleDataAdapter(command))
                        {
                            // adapter.Fill is synchronous, run it in a background thread.
                            await Task.Run(() => adapter.Fill(dataTable));
                        }
                        stopwatch.Stop();
                        Logger.LogInfo($"ExecuteQueryAsync completed in {stopwatch.ElapsedMilliseconds}ms. Rows returned: {dataTable.Rows.Count}. SQL: {sanitizedSql}");
                    }
                }
            }
            catch (OracleException oraEx)
            {
                stopwatch.Stop();
                Logger.LogError($"OracleException in ExecuteQueryAsync (Duration: {stopwatch.ElapsedMilliseconds}ms). SQL: {sanitizedSql}", oraEx);
                throw; // Re-throw to allow caller to handle
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.LogError($"Generic Exception in ExecuteQueryAsync (Duration: {stopwatch.ElapsedMilliseconds}ms). SQL: {sanitizedSql}", ex);
                throw; // Re-throw
            }
            return dataTable;
        }

        public async Task ExecuteNonQueryAsync(OracleConnection connection, OracleTransaction transaction, string sql)
        {
            var stopwatch = new Stopwatch();
            string sanitizedSql = SanitizeSqlForLogging(sql);
            Logger.LogInfo($"ExecuteNonQueryAsync starting. SQL: {sanitizedSql}");
            int rowsAffected = 0;

            // Connection is managed by the caller (opened, transaction started)
            if (connection == null || connection.State != ConnectionState.Open)
            {
                Logger.LogError("ExecuteNonQueryAsync called with null or closed connection.", new ArgumentNullException(nameof(connection)));
                throw new ArgumentNullException(nameof(connection), "Connection must be provided and open.");
            }
            if (transaction == null)
            {
                Logger.LogError("ExecuteNonQueryAsync called with null transaction.", new ArgumentNullException(nameof(transaction)));
                throw new ArgumentNullException(nameof(transaction), "Transaction must be provided.");
            }

            try
            {
                using (var command = new OracleCommand(sql, connection))
                {
                    command.Transaction = transaction;
                    stopwatch.Start();
                    rowsAffected = await command.ExecuteNonQueryAsync();
                    stopwatch.Stop();
                    Logger.LogInfo($"ExecuteNonQueryAsync completed in {stopwatch.ElapsedMilliseconds}ms. Rows affected: {rowsAffected}. SQL: {sanitizedSql}");
                }
            }
            catch (OracleException oraEx)
            {
                stopwatch.Stop();
                Logger.LogError($"OracleException in ExecuteNonQueryAsync (Duration: {stopwatch.ElapsedMilliseconds}ms). SQL: {sanitizedSql}", oraEx);
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.LogError($"Generic Exception in ExecuteNonQueryAsync (Duration: {stopwatch.ElapsedMilliseconds}ms). SQL: {sanitizedSql}", ex);
                throw;
            }
        }

        public async Task<DataTable> ExecuteQueryWithinTransactionAsync(OracleConnection connection, OracleTransaction? transaction, string sql)
        {
            var dataTable = new DataTable();
            var stopwatch = new Stopwatch();
            string sanitizedSql = SanitizeSqlForLogging(sql);
            Logger.LogInfo($"ExecuteQueryWithinTransactionAsync starting. SQL: {sanitizedSql}");

            // Connection is managed by the caller
            if (connection == null || connection.State != ConnectionState.Open)
            {
                Logger.LogError("ExecuteQueryWithinTransactionAsync called with null or closed connection.", new ArgumentNullException(nameof(connection)));
                throw new ArgumentNullException(nameof(connection), "Connection must be provided and open.");
            }
            // Transaction can be null if the caller intends to run it outside a transaction, though risky for consistency.
            // However, our current MainForm usage always provides a transaction.

            try
            {
                using (var command = new OracleCommand(sql, connection))
                {
                    command.Transaction = transaction;
                    stopwatch.Start();
                    using (var adapter = new OracleDataAdapter(command))
                    {
                        await Task.Run(() => adapter.Fill(dataTable));
                    }
                    stopwatch.Stop();
                    Logger.LogInfo($"ExecuteQueryWithinTransactionAsync completed in {stopwatch.ElapsedMilliseconds}ms. Rows returned: {dataTable.Rows.Count}. SQL: {sanitizedSql}");
                }
            }
            catch (OracleException oraEx)
            {
                stopwatch.Stop();
                Logger.LogError($"OracleException in ExecuteQueryWithinTransactionAsync (Duration: {stopwatch.ElapsedMilliseconds}ms). SQL: {sanitizedSql}", oraEx);
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.LogError($"Generic Exception in ExecuteQueryWithinTransactionAsync (Duration: {stopwatch.ElapsedMilliseconds}ms). SQL: {sanitizedSql}", ex);
                throw;
            }
            return dataTable;
        }
    }
}