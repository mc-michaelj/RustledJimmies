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
            var response = await _httpClient.PostAsync($"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={_apiKey}", content);

            // Log the request and response details
            System.Diagnostics.Debug.WriteLine($"Request URL: https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={_apiKey}");
            System.Diagnostics.Debug.WriteLine($"Request Body: {JsonConvert.SerializeObject(requestBody)}");
            System.Diagnostics.Debug.WriteLine($"Response Status: {response.StatusCode}");
            var responseContent = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"Response Content: {responseContent}");

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Error calling Gemini API: {response.StatusCode}. Details: {responseContent}");
            }

            // The actual JSON content is nested inside the API response. We need to parse it out.
            dynamic? parsedJson = JsonConvert.DeserializeObject<dynamic>(responseContent);
            string? modelResponseText = parsedJson?.candidates[0]?.content?.parts[0]?.text?.ToString();

            if (string.IsNullOrWhiteSpace(modelResponseText))
            {
                throw new Exception("Gemini returned an empty or invalid response body.");
            }

            // The AI may add conversational text and markdown. Find the JSON block.
            string jsonBlock = modelResponseText;
            string jsonMarker = "```json";
            int startIndex = modelResponseText.IndexOf(jsonMarker);

            if (startIndex != -1)
            {
                startIndex += jsonMarker.Length;
                int endIndex = modelResponseText.LastIndexOf("```");
                if (endIndex > startIndex)
                {
                    jsonBlock = modelResponseText.Substring(startIndex, endIndex - startIndex).Trim();
                }
            }
            else
            {
                // Fallback to finding the first and last brace if no markdown block is found
                int firstBrace = modelResponseText.IndexOf('{');
                int lastBrace = modelResponseText.LastIndexOf('}');

                if (firstBrace != -1 && lastBrace > firstBrace)
                {
                    jsonBlock = modelResponseText.Substring(firstBrace, lastBrace - firstBrace + 1);
                }
                else
                {
                    throw new Exception("Could not find a valid JSON object in the Gemini response.");
                }
            }

            try
            {
                return JsonConvert.DeserializeObject<GeminiResponse>(jsonBlock);
            }
            catch (JsonReaderException ex)
            {
                // Throw a more specific exception to help with debugging
                throw new JsonReaderException($"Failed to parse the following JSON block from Gemini: {jsonBlock}", ex);
            }
        }

        public async Task<string> GetTableSchemaFromGemini(string sqlScript, string modelName)
        {
            string prompt = $"Based on the following SQL script, return a JSON object describing the tables involved. The JSON should be an array where each object has 'tableName', and an array of 'columns' with 'columnName' and 'dataType' (use Oracle types like VARCHAR2, NUMBER, DATE). SQL Script: {sqlScript}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={_apiKey}", content);

            // Log the request and response details
            System.Diagnostics.Debug.WriteLine($"Request URL (GetTableSchemaFromGemini): https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={_apiKey}");
            System.Diagnostics.Debug.WriteLine($"Request Body (GetTableSchemaFromGemini): {JsonConvert.SerializeObject(requestBody)}");
            System.Diagnostics.Debug.WriteLine($"Response Status (GetTableSchemaFromGemini): {response.StatusCode}");
            var responseContent = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"Response Content (GetTableSchemaFromGemini): {responseContent}");

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Error calling Gemini API (GetTableSchemaFromGemini): {response.StatusCode}. Details: {responseContent}");
            }

            dynamic? parsedJson = JsonConvert.DeserializeObject<dynamic>(responseContent);
            string? modelResponseText = parsedJson?.candidates[0]?.content?.parts[0]?.text?.ToString();

            if (string.IsNullOrWhiteSpace(modelResponseText))
            {
                throw new Exception("Gemini returned an empty or invalid response for GetTableSchemaFromGemini.");
            }

            // Unlike AnalyzeSqlAsync, we expect the response to be the JSON string directly,
            // as requested in the prompt. We might still need to trim markdown if Gemini wraps it.
            string jsonResult = modelResponseText.Trim();
            string jsonMarker = "```json";
            if (jsonResult.StartsWith(jsonMarker))
            {
                jsonResult = jsonResult.Substring(jsonMarker.Length);
                if (jsonResult.EndsWith("```"))
                {
                    jsonResult = jsonResult.Substring(0, jsonResult.Length - 3);
                }
                jsonResult = jsonResult.Trim();
            }
            // A simpler check if it's just JSON without markdown, in case the above is too aggressive or unnecessary
            else if (jsonResult.StartsWith("{") && jsonResult.EndsWith("}") || jsonResult.StartsWith("[") && jsonResult.EndsWith("]"))
            {
                // It's likely plain JSON, do nothing further for stripping
            }
            else
            {
                // If it's not wrapped in markdown and doesn't look like JSON, it might be an error or unexpected format.
                // For now, we return it as is, but this could be a point for more robust error handling.
                System.Diagnostics.Debug.WriteLine($"Warning (GetTableSchemaFromGemini): Response doesn't appear to be clean JSON or markdown-wrapped JSON: {jsonResult}");
            }

            if (string.IsNullOrWhiteSpace(jsonResult))
            {
                throw new Exception("Extracted JSON string is empty or invalid after attempting to clean response from GetTableSchemaFromGemini.");
            }

            return jsonResult;
        }
    }
}