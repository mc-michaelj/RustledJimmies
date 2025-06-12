using System;
using System.Data;
using Oracle.ManagedDataAccess.Client; // This using statement will be added

namespace OracleOptimizer.Services;

public class OracleDatabaseService : IDatabaseService
{
    public DataTable ExecuteQuery(string connectionString, string sql)
    {
        try
        {
            using (OracleConnection connection = new OracleConnection(connectionString))
            {
                connection.Open();
                using (OracleCommand command = new OracleCommand(sql, connection))
                {
                    using (OracleDataAdapter adapter = new OracleDataAdapter(command))
                    {
                        DataTable dataTable = new DataTable();
                        adapter.Fill(dataTable);
                        return dataTable;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // In a real application, log this exception
            Console.WriteLine($"Error executing query: {ex.Message}");
            throw; // Re-throw the exception to be handled by the ViewModel
        }
    }

    public void ExecuteNonQuery(string connectionString, string sql)
    {
        try
        {
            using (OracleConnection connection = new OracleConnection(connectionString))
            {
                connection.Open();
                using (OracleCommand command = new OracleCommand(sql, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }
        catch (Exception ex)
        {
            // In a real application, log this exception
            Console.WriteLine($"Error executing non-query: {ex.Message}");
            throw; // Re-throw the exception to be handled by the ViewModel
        }
    }
}
