using Oracle.ManagedDataAccess.Client;
using System;
using System.Data;
using System.Threading.Tasks;

namespace OracleOptimizer
{
    public class OracleService
    {
        /// <summary>
        /// Executes a SQL query and returns the results as a DataTable.
        /// </summary>
        /// <param name="connectionString">The Oracle connection string.</param>
        /// <param name="query">The SQL query to execute.</param>
        /// <returns>A DataTable containing the query results.</returns>
        public async Task<DataTable> ExecuteQueryAsync(string connectionString, string query)
        {
            var dataTable = new DataTable();
            try
            {
                using (var connection = new OracleConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            dataTable.Load(reader);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Consider logging the exception or re-throwing a custom exception
                Console.WriteLine($"Error executing query: {ex.Message}");
                // Return an empty DataTable or throw to indicate failure
                // For now, rethrow to make it visible to the caller
                throw;
            }
            return dataTable;
        }

        /// <summary>
        /// Executes a PL/SQL procedure body within a given transaction.
        /// </summary>
        /// <param name="connection">An open OracleConnection.</param>
        /// <param name="transaction">An active OracleTransaction.</param>
        /// <param name="procedureBody">The PL/SQL procedure body to execute.</param>
        public async Task ExecuteProcedureAsync(OracleConnection connection, OracleTransaction transaction, string procedureBody)
        {
            if (connection == null || connection.State != ConnectionState.Open)
            {
                throw new ArgumentException("Connection must be open and not null.", nameof(connection));
            }
            if (transaction == null)
            {
                throw new ArgumentNullException(nameof(transaction), "Transaction cannot be null.");
            }

            try
            {
                using (var command = new OracleCommand(procedureBody, connection))
                {
                    command.Transaction = transaction;
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                // Consider logging the exception
                Console.WriteLine($"Error executing procedure: {ex.Message}");
                // Rethrow to allow the calling code to handle transaction rollback
                throw;
            }
        }
    }
}
