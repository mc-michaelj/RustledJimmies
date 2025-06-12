using System;
using System.Data;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types; // May not be strictly needed for these methods but good to have for Oracle development

namespace IntelligentOracleSQLOptimizer.Services
{
    public class OracleService
    {
        /// <summary>
        /// Executes a query and returns a DataTable.
        /// If a transaction is provided, it uses the transaction's connection; otherwise, it manages its own connection.
        /// </summary>
        public async Task<DataTable> ExecuteQueryAsync(string connectionString, string query, OracleTransaction? transaction = null)
        {
            DataTable dataTable = new DataTable();
            OracleConnection? ownConnection = null;
            OracleCommand command;

            try
            {
                if (transaction != null)
                {
                    if (transaction.Connection == null)
                    {
                        throw new ArgumentException("Transaction does not have a valid connection.", nameof(transaction));
                    }
                    command = new OracleCommand(query, transaction.Connection)
                    {
                        Transaction = transaction
                    };
                }
                else
                {
                    ownConnection = new OracleConnection(connectionString);
                    await ownConnection.OpenAsync();
                    command = new OracleCommand(query, ownConnection);
                }

                using (command) // Ensure command is disposed
                using (OracleDataAdapter adapter = new OracleDataAdapter(command))
                {
                    await Task.Run(() => adapter.Fill(dataTable)); // Fill is synchronous, run in a task for async pattern
                }
            }
            catch (Exception ex)
            {
                // Log or handle more specifically if needed, then rethrow or wrap
                // For now, let the caller (MainForm) handle the display of Oracle exceptions
                // Consider logging ex.ToString() for detailed diagnostics if this service were more complex
                throw;
            }
            finally
            {
                if (ownConnection != null && ownConnection.State == ConnectionState.Open)
                {
                    await ownConnection.CloseAsync();
                    await ownConnection.DisposeAsync();
                }
            }
            return dataTable;
        }

        /// <summary>
        /// Executes a non-query SQL statement (e.g., PL/SQL block, DML without result).
        /// If a transaction is provided, it uses the transaction's connection; otherwise, it manages its own connection.
        /// </summary>
        public async Task ExecuteNonQueryAsync(string connectionString, string sql, OracleTransaction? transaction = null)
        {
            OracleConnection? ownConnection = null;
            OracleCommand command;

            try
            {
                if (transaction != null)
                {
                     if (transaction.Connection == null)
                    {
                        throw new ArgumentException("Transaction does not have a valid connection.", nameof(transaction));
                    }
                    command = new OracleCommand(sql, transaction.Connection)
                    {
                        Transaction = transaction
                    };
                }
                else
                {
                    ownConnection = new OracleConnection(connectionString);
                    await ownConnection.OpenAsync();
                    command = new OracleCommand(sql, ownConnection);
                }

                using (command) // Ensure command is disposed
                {
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                // Log or handle, then rethrow or wrap
                throw;
            }
            finally
            {
                if (ownConnection != null && ownConnection.State == ConnectionState.Open)
                {
                    await ownConnection.CloseAsync();
                    await ownConnection.DisposeAsync(); // Use DisposeAsync for OracleConnection
                }
            }
        }
    }
}
