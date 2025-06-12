using System.Data;

namespace OracleOptimizer.Services;

public interface IDatabaseService
{
    DataTable ExecuteQuery(string connectionString, string sql);
    void ExecuteNonQuery(string connectionString, string sql);
}
