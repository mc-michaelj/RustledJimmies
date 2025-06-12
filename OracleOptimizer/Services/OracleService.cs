using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Threading.Tasks;

namespace OracleOptimizer.Services
{
    public class OracleService
    {
        public async Task<DataTable> ExecuteQueryAsync(string connectionString, string sql)
        {
            var dataTable = new DataTable();
            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand(sql, connection))
                {
                    using (var adapter = new OracleDataAdapter(command))
                    {
                        await Task.Run(() => adapter.Fill(dataTable));
                    }
                }
            }
            return dataTable;
        }

        public async Task ExecuteNonQueryAsync(OracleConnection connection, OracleTransaction transaction, string sql)
        {
            using (var command = new OracleCommand(sql, connection))
            {
                command.Transaction = transaction;
                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task<DataTable> ExecuteQueryWithinTransactionAsync(OracleConnection connection, OracleTransaction transaction, string sql)
        {
            var dataTable = new DataTable();
            using (var command = new OracleCommand(sql, connection))
            {
                command.Transaction = transaction;
                using (var adapter = new OracleDataAdapter(command))
                {
                    await Task.Run(() => adapter.Fill(dataTable));
                }
            }
            return dataTable;
        }
    }
}