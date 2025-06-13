using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Threading.Tasks;

namespace OracleOptimizer.Services
{
    /// <summary>
    /// Provides services for interacting with an Oracle database.
    /// This includes executing queries and non-query commands.
    /// </summary>
    public class OracleService
    {
        /// <summary>
        /// Executes a SQL query against the Oracle database and returns the results as a DataTable.
        /// This method handles opening and closing the database connection.
        /// </summary>
        /// <param name="connectionString">The Oracle database connection string.</param>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="commandTimeout">Optional command timeout in seconds. If null, the default timeout for the provider is used.</param>
        /// <returns>A Task that represents the asynchronous operation.
        /// The task result contains a DataTable populated with the query results.</returns>
        public async Task<DataTable> ExecuteQueryAsync(string connectionString, string sql, int? commandTimeout = null)
        {
            var dataTable = new DataTable();
            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand(sql, connection))
                {
                    if (commandTimeout.HasValue)
                    {
                        command.CommandTimeout = commandTimeout.Value;
                    }
                    using (var adapter = new OracleDataAdapter(command))
                    {
                        await Task.Run(() => adapter.Fill(dataTable));
                    }
                }
            }
            return dataTable;
        }

        /// <summary>
        /// Executes a SQL non-query command (e.g., INSERT, UPDATE, DELETE, PL/SQL block)
        /// against the Oracle database using an existing open connection and transaction.
        /// </summary>
        /// <param name="connection">An open OracleConnection to use for executing the command.</param>
        /// <param name="transaction">An OracleTransaction to associate with the command.</param>
        /// <param name="sql">The SQL command or PL/SQL block to execute.</param>
        /// <param name="commandTimeout">Optional command timeout in seconds. If null, the default timeout for the provider is used.</param>
        /// <returns>A Task that represents the asynchronous operation.</returns>
        public async Task ExecuteNonQueryAsync(OracleConnection connection, OracleTransaction transaction, string sql, int? commandTimeout = null)
        {
            using (var command = new OracleCommand(sql, connection))
            {
                command.Transaction = transaction;
                if (commandTimeout.HasValue)
                {
                    command.CommandTimeout = commandTimeout.Value;
                }
                await command.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Executes a SQL query against the Oracle database using an existing open connection and transaction,
        /// and returns the results as a DataTable.
        /// </summary>
        /// <param name="connection">An open OracleConnection to use for executing the query.</param>
        /// <param name="transaction">An optional OracleTransaction to associate with the query. Can be null.</param>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="commandTimeout">Optional command timeout in seconds. If null, the default timeout for the provider is used.</param>
        /// <returns>A Task that represents the asynchronous operation.
        /// The task result contains a DataTable populated with the query results.</returns>
        public async Task<DataTable> ExecuteQueryWithinTransactionAsync(OracleConnection connection, OracleTransaction? transaction, string sql, int? commandTimeout = null)
        {
            var dataTable = new DataTable();
            using (var command = new OracleCommand(sql, connection))
            {
                command.Transaction = transaction;
                if (commandTimeout.HasValue)
                {
                    command.CommandTimeout = commandTimeout.Value;
                }
                using (var adapter = new OracleDataAdapter(command))
                {
                    await Task.Run(() => adapter.Fill(dataTable));
                }
            }
            return dataTable;
        }
    }
}