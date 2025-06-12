using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

// Assuming GeminiAnalysisResult is in the root namespace or Models namespace
// If it were in Models: using IntelligentOracleSQLOptimizer.Models;

namespace IntelligentOracleSQLOptimizer.Services
{
    // Helper class for Gemini Request Payload
    internal class GeminiRequestPayload
    {
        [JsonPropertyName("contents")]
        public Content[] Contents { get; set; }
    }

    internal class Content
    {
        [JsonPropertyName("parts")]
        public Part[] Parts { get; set; }
    }

    internal class Part
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }
    }

    // Helper class for Gemini API Response (structure can be complex)
    // This needs to match the actual Gemini API response structure to extract the content.
    // This is a simplified version focusing on getting the text part.
    internal class GeminiApiResponse
    {
        [JsonPropertyName("candidates")]
        public Candidate[] Candidates { get; set; }

        // Sometimes errors are directly in the response body outside candidates
        [JsonPropertyName("error")]
        public GeminiError Error {get; set;}
    }

    internal class GeminiError
    {
        [JsonPropertyName("message")]
        public string Message {get; set;}
    }


    internal class Candidate
    {
        [JsonPropertyName("content")]
        public Content Content { get; set; }
    }
    // GeminiAnalysisResult is defined in IntelligentOracleSQLOptimizer/GeminiAnalysisResult.cs

    public class GeminiService
    {
        // It's better to use IHttpClientFactory in real applications,
        // but for a standalone desktop app, a static HttpClient is often acceptable.
        private static readonly HttpClient client = new HttpClient();

        public async Task<GeminiAnalysisResult> AnalyzeSqlAsync(string procedureBody, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "YOUR_GEMINI_API_KEY")
            {
                return new GeminiAnalysisResult { Error = "Gemini API key is not configured. Please provide a valid API key." };
            }

            string masterPrompt = $@"
You are an expert Oracle SQL Performance Tuning Specialist and a meticulous Test Planner. Your task is to analyze the following Oracle PL/SQL procedure and provide a detailed, structured JSON response containing a complete plan for optimizing and validating it.

Here is the procedure to analyze:
{procedureBody}

Your Response MUST be a JSON object with the following exact structure and keys (ensure JSON is valid, strings are properly escaped):
{{
  ""validation_query_before"": ""A single, runnable SELECT statement that queries the state of the data that will be affected by this procedure before it runs."",
  ""optimized_procedure_body"": ""The complete, rewritten PL/SQL procedure body. You must replace inefficient cursors and loops with high-performance, set-based statements."",
  ""validation_query_after"": ""A single, runnable SELECT statement that queries the state of the data after the procedure has run."",
  ""explanation"": ""A brief, clear explanation of the changes you made.""
}}";

            // Example Gemini API endpoint - replace with the actual one if different
            string apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-pro:generateContent?key={apiKey}";

            var requestPayload = new GeminiRequestPayload
            {
                Contents = new Content[]
                {
                    new Content { Parts = new Part[] { new Part { Text = masterPrompt } } }
                }
            };

            try
            {
                string jsonPayload = JsonSerializer.Serialize(requestPayload);
                var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // It's good practice to set a timeout on HttpClient if not configured globally
                // client.Timeout = TimeSpan.FromSeconds(90); // Example

                HttpResponseMessage response = await client.PostAsync(apiUrl, httpContent);

                string jsonResponse = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true // Helpful if Gemini casing differs slightly
                    };
                    GeminiApiResponse geminiResponse = JsonSerializer.Deserialize<GeminiApiResponse>(jsonResponse, options);

                    if (geminiResponse?.Candidates != null && geminiResponse.Candidates.Length > 0 &&
                        geminiResponse.Candidates[0].Content?.Parts != null && geminiResponse.Candidates[0].Content.Parts.Length > 0)
                    {
                        string extractedJsonPlan = geminiResponse.Candidates[0].Content.Parts[0].Text;

                        // The extractedJsonPlan should be the JSON string we asked for.
                        // Now deserialize this into GeminiAnalysisResult
                        try
                        {
                             // Need to ensure property names in GeminiAnalysisResult match the prompt's JSON keys
                            GeminiAnalysisResult analysisResult = JsonSerializer.Deserialize<GeminiAnalysisResult>(extractedJsonPlan, options);
                            if (analysisResult == null) {
                                return new GeminiAnalysisResult { Error = "Failed to deserialize the extracted JSON plan from Gemini response."};
                            }
                            // Check for nulls which might indicate Gemini didn't follow the prompt fully
                            if (string.IsNullOrWhiteSpace(analysisResult.optimized_procedure_body) ||
                                string.IsNullOrWhiteSpace(analysisResult.validation_query_before) ||
                                string.IsNullOrWhiteSpace(analysisResult.validation_query_after) ||
                                string.IsNullOrWhiteSpace(analysisResult.explanation))
                            {
                                analysisResult.Error = "Gemini response was parsed, but one or more required fields are missing. Prompt may need adjustment or model did not fully comply.";
                                // still return the partial data if any
                            }
                            return analysisResult;
                        }
                        catch (JsonException jsonEx)
                        {
                            return new GeminiAnalysisResult { Error = $"Error deserializing the extracted JSON plan from Gemini: {jsonEx.Message}. Extracted text: {extractedJsonPlan}" };
                        }
                    }
                    else if (geminiResponse?.Error?.Message != null) {
                         return new GeminiAnalysisResult { Error = $"Gemini API returned an error: {geminiResponse.Error.Message}" };
                    }
                    else
                    {
                        return new GeminiAnalysisResult { Error = "Gemini response was successful, but the expected content structure was not found. Raw response: " + jsonResponse.Substring(0, Math.Min(500, jsonResponse.Length)) };
                    }
                }
                else
                {
                    // Try to parse error from Gemini's response if possible
                    try {
                        GeminiApiResponse errorResponse = JsonSerializer.Deserialize<GeminiApiResponse>(jsonResponse);
                        if (errorResponse?.Error?.Message != null) {
                             return new GeminiAnalysisResult { Error = $"API request failed: {response.StatusCode} - {errorResponse.Error.Message}" };
                        }
                    } catch {} // Ignore if parsing error response fails, just use status code and raw content.
                    return new GeminiAnalysisResult { Error = $"API request failed: {response.StatusCode} - {jsonResponse.Substring(0, Math.Min(500, jsonResponse.Length))}" };
                }
            }
            catch (HttpRequestException httpEx)
            {
                return new GeminiAnalysisResult { Error = $"HTTP request error: {httpEx.Message}" };
            }
            catch (JsonException jsonEx)
            {
                return new GeminiAnalysisResult { Error = $"JSON serialization/deserialization error: {jsonEx.Message}" };
            }
            catch (Exception ex) // Catch-all for other unexpected errors
            {
                return new GeminiAnalysisResult { Error = $"An unexpected error occurred in GeminiService: {ex.Message}" };
            }
        }
    }
}
