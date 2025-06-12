using OracleOptimizer.Models;
using System.Threading.Tasks;

namespace OracleOptimizer.Services;

public interface IGeminiApiService
{
    Task<GeminiApiResponse> AnalyzeSqlScriptAsync(string userSqlScript, string apiKey);
}
