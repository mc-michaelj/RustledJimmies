namespace IntelligentOracleSQLOptimizer
{
    public class GeminiAnalysisResult
    {
        // Properties expected from Gemini's structured JSON response
        // These names must match the JSON keys in the master prompt
        public string? validation_query_before { get; set; }
        public string? optimized_procedure_body { get; set; }
        public string? validation_query_after { get; set; }
        public string? explanation { get; set; }

        // Property for communication errors or API errors during the call to Gemini Service itself
        public string? Error { get; set; }
    }
}
