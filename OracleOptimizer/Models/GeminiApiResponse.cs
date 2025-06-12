using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace OracleOptimizer.Models;

public record GeminiApiResponse(
    [property: JsonPropertyName("optimized_sql")] string OptimizedSql,
    [property: JsonPropertyName("explanation")] string Explanation,
    [property: JsonPropertyName("validation_queries")] List<string> ValidationQueries
);
