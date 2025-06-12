using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace OracleOptimizer.Services
{
    public class GeminiResponse
    {
        [JsonProperty("validation_query_before")]
        public string? ValidationQueryBefore { get; set; }

        [JsonProperty("optimized_procedure_body")]
        public string? OptimizedProcedureBody { get; set; }

        [JsonProperty("validation_query_after")]
        public string? ValidationQueryAfter { get; set; }

        [JsonProperty("explanation")]
        public string? Explanation { get; set; }
    }

    public class GeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public GeminiService(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<GeminiResponse?> AnalyzeSqlAsync(string procedureBody, string modelName)
        {
            string masterPrompt = @"You are an expert Oracle SQL Performance Tuning Specialist and a meticulous Test Planner. Your task is to analyze the following Oracle PL/SQL procedure and provide a detailed, structured JSON response containing a complete plan for optimizing and validating it.

Here is the procedure to analyze:
{user's_procedure_body_goes_here}

Your Response MUST be a JSON object with the following exact structure and keys:
{
    ""validation_query_before"": ""SELECT statement to get the data state before the logic runs"",
    ""optimized_procedure_body"": ""The full, optimized PL/SQL code"",
    ""validation_query_after"": ""SELECT statement to get the data state after the logic runs"",
    ""explanation"": ""A detailed explanation of the optimizations and validation plan""
}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = masterPrompt.Replace("{user's_procedure_body_goes_here}", procedureBody) }
                        }
                    }
                }
            };

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"https://generativelanguage.googleapis.com/v1beta/models/{modelName}/generateContent?key={_apiKey}", content);

            // Log the request and response details
            System.Diagnostics.Debug.WriteLine($"Request URL: https://generativelanguage.googleapis.com/v1beta/models/{modelName}/generateContent?key={_apiKey}");
            System.Diagnostics.Debug.WriteLine($"Request Body: {JsonConvert.SerializeObject(requestBody)}");
            System.Diagnostics.Debug.WriteLine($"Response Status: {response.StatusCode}");
            var responseContent = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"Response Content: {responseContent}");

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Error calling Gemini API: {response.StatusCode}. Details: {responseContent}");
            }

            return JsonConvert.DeserializeObject<GeminiResponse>(responseContent);
        }
    }
}