using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using OracleOptimizer; // Added for Logger

namespace OracleOptimizer.Services
{
    // TODO: Consider moving GeminiResponse to a separate file if it grows or if more response types are added.
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
        // TODO: Make API version and base URL configurable if necessary, e.g., via app settings.
        private const string GeminiApiBaseUrl = "https://generativelanguage.googleapis.com";
        private const string GeminiApiVersion = "v1beta";

        public GeminiService(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                // Log this critical configuration error.
                Logger.LogError("GeminiService initialized with a null or empty API key.", new ArgumentNullException(nameof(apiKey)));
                throw new ArgumentNullException(nameof(apiKey), "API key cannot be null or empty.");
            }
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private string BuildApiUrl(string modelName, string action = "generateContent")
        {
            // API key is now added as a query parameter in the PostAsync call to avoid logging it if the URL itself is logged.
            return $"{GeminiApiBaseUrl}/{GeminiApiVersion}/models/{modelName}:{action}";
        }

        public async Task<GeminiResponse?> AnalyzeSqlAsync(string procedureBody, string modelName)
        {
            // TODO: Consider externalizing complex prompts to resource files or a configuration system.
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
            string apiUrl = BuildApiUrl(modelName) + $"?key={_apiKey}"; // API key added here for the actual request

            Logger.LogInfo($"Sending API request to Gemini for SQL analysis. Model: {modelName}. API URL (key excluded): {BuildApiUrl(modelName)}");
            Logger.LogInfo($"Request Body (AnalyzeSqlAsync): {JsonConvert.SerializeObject(requestBody)}"); // Be cautious if procedureBody could be very large

            HttpResponseMessage response;
            string responseContent;
            try
            {
                response = await _httpClient.PostAsync(apiUrl, content);
                responseContent = await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException httpEx)
            {
                Logger.LogError($"HTTP request failed when calling Gemini API for SQL analysis. Model: {modelName}.", httpEx);
                throw; // Re-throw to allow MainForm to handle and display a user-friendly message
            }

            Logger.LogInfo($"Response Status (AnalyzeSqlAsync): {response.StatusCode}");
            if (!response.IsSuccessStatusCode)
            {
                Logger.LogError($"Error calling Gemini API for SQL analysis: {response.StatusCode}. Model: {modelName}. Details: {responseContent}");
                throw new HttpRequestException($"Error calling Gemini API: {response.StatusCode}. Details: {responseContent}");
            }
            Logger.LogInfo($"Raw Response Content (AnalyzeSqlAsync): {responseContent}"); // Log raw response

            dynamic? parsedJson = null;
            try
            {
                parsedJson = JsonConvert.DeserializeObject<dynamic>(responseContent);
            }
            catch (JsonException jsonEx)
            {
                Logger.LogError($"Failed to parse initial JSON response from Gemini (AnalyzeSqlAsync). Content: {responseContent}", jsonEx);
                throw new InvalidOperationException("Gemini returned a response that could not be parsed as initial JSON.", jsonEx);
            }

            string? modelResponseText = parsedJson?.candidates[0]?.content?.parts[0]?.text?.ToString();

            if (string.IsNullOrWhiteSpace(modelResponseText))
            {
                Logger.LogError($"Gemini returned an empty or invalid model response text (AnalyzeSqlAsync). Full response: {responseContent}");
                throw new InvalidOperationException("Gemini returned an empty or invalid response body for SQL analysis.");
            }
            Logger.LogInfo($"Model Response Text (AnalyzeSqlAsync - pre-cleaning): {modelResponseText}");

            string jsonBlock = ExtractJsonBlock(modelResponseText, "AnalyzeSqlAsync");

            try
            {
                return JsonConvert.DeserializeObject<GeminiResponse>(jsonBlock);
            }
            catch (JsonException ex) // Catch JsonException for broader issues including JsonReaderException
            {
                Logger.LogError($"Failed to parse the extracted JSON block from Gemini (AnalyzeSqlAsync). Extracted JSON: {jsonBlock}", ex);
                // Throw a new exception to ensure the problematic JSON is part of the message if it's not too long.
                string loggedJsonBlock = jsonBlock.Length > 500 ? jsonBlock.Substring(0, 500) + "..." : jsonBlock;
                throw new InvalidOperationException($"Failed to parse the JSON block from Gemini: {loggedJsonBlock}", ex);
            }
        }

        private string ExtractJsonBlock(string modelResponseText, string callingMethodName)
        {
            Logger.LogInfo($"Extracting JSON block for {callingMethodName}. Input text length: {modelResponseText.Length}");
            string jsonBlock = modelResponseText.Trim();
            string jsonMarkerStart = "```json";
            string jsonMarkerEnd = "```";

            int startIndex = jsonBlock.IndexOf(jsonMarkerStart);
            if (startIndex != -1)
            {
                startIndex += jsonMarkerStart.Length;
                int endIndex = jsonBlock.LastIndexOf(jsonMarkerEnd);
                if (endIndex > startIndex)
                {
                    jsonBlock = jsonBlock.Substring(startIndex, endIndex - startIndex).Trim();
                    Logger.LogInfo($"Extracted JSON using markdown markers for {callingMethodName}.");
                }
                else
                {
                    Logger.LogWarn($"Found start markdown marker but no valid end marker for {callingMethodName}. Attempting to use content after marker anyway.");
                    jsonBlock = jsonBlock.Substring(startIndex).Trim();
                }
            }
            else if (jsonBlock.StartsWith("{") && jsonBlock.EndsWith("}") || jsonBlock.StartsWith("[") && jsonBlock.EndsWith("]"))
            {
                 Logger.LogInfo($"JSON block for {callingMethodName} seems to be plain JSON (no markdown markers).");
            }
            else
            {
                Logger.LogWarn($"Could not find JSON markdown markers or direct JSON structure for {callingMethodName}. The response might be malformed or plain text. Content (first 200 chars): {modelResponseText.Substring(0, Math.Min(modelResponseText.Length,200))}");
                // Attempting to find first '{' and last '}' as a fallback
                int firstBrace = jsonBlock.IndexOf('{');
                int lastBrace = jsonBlock.LastIndexOf('}');
                if (firstBrace != -1 && lastBrace > firstBrace)
                {
                    jsonBlock = jsonBlock.Substring(firstBrace, lastBrace - firstBrace + 1).Trim();
                    Logger.LogInfo($"Extracted JSON using fallback brace finding for {callingMethodName}.");
                }
                else
                {
                     Logger.LogError($"Could not extract a valid JSON object using any method for {callingMethodName}. Original text (first 200 chars): {modelResponseText.Substring(0, Math.Min(modelResponseText.Length,200))}");
                    throw new InvalidOperationException($"Could not find a valid JSON object in the Gemini response for {callingMethodName}.");
                }
            }
            return jsonBlock;
        }

        public async Task<string> GetTableSchemaFromGemini(string sqlScript, string modelName)
        {
            // TODO: Consider externalizing complex prompts to resource files or a configuration system.
            string prompt = @"Based on the following SQL script, return a JSON object describing the tables involved. 
The JSON must be an array where each object has 'tableName' and an array of 'columns' with 'columnName' and 'dataType' (use Oracle types like VARCHAR2, NUMBER, DATE).
For each 'tableName', you MUST provide the fully qualified name, including the schema (e.g., 'MYSCHEMA.MYTABLE'). If a table is used without a schema in the script, you must still identify and include its correct schema in the response.
This is critical to avoid 'table not found' errors during testing.
SQL Script: {sqlScript}";

            prompt = prompt.Replace("{sqlScript}", sqlScript);

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
            string apiUrl = BuildApiUrl(modelName) + $"?key={_apiKey}";

            Logger.LogInfo($"Sending API request to Gemini for table schema. Model: {modelName}. API URL (key excluded): {BuildApiUrl(modelName)}");
            Logger.LogInfo($"Request Body (GetTableSchemaFromGemini): {JsonConvert.SerializeObject(requestBody)}"); // Be cautious if sqlScript could be very large

            HttpResponseMessage response;
            string responseContent;
            try
            {
                response = await _httpClient.PostAsync(apiUrl, content);
                responseContent = await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException httpEx)
            {
                Logger.LogError($"HTTP request failed when calling Gemini API for table schema. Model: {modelName}.", httpEx);
                throw; // Re-throw
            }

            Logger.LogInfo($"Response Status (GetTableSchemaFromGemini): {response.StatusCode}");
            if (!response.IsSuccessStatusCode)
            {
                Logger.LogError($"Error calling Gemini API for table schema: {response.StatusCode}. Model: {modelName}. Details: {responseContent}");
                throw new HttpRequestException($"Error calling Gemini API (GetTableSchemaFromGemini): {response.StatusCode}. Details: {responseContent}");
            }
            Logger.LogInfo($"Raw Response Content (GetTableSchemaFromGemini): {responseContent}");

            dynamic? parsedJson = null;
            try
            {
                 parsedJson = JsonConvert.DeserializeObject<dynamic>(responseContent);
            }
            catch (JsonException jsonEx)
            {
                Logger.LogError($"Failed to parse initial JSON response from Gemini (GetTableSchemaFromGemini). Content: {responseContent}", jsonEx);
                throw new InvalidOperationException("Gemini returned a response that could not be parsed as initial JSON for table schema.", jsonEx);
            }

            string? modelResponseText = parsedJson?.candidates[0]?.content?.parts[0]?.text?.ToString();

            if (string.IsNullOrWhiteSpace(modelResponseText))
            {
                Logger.LogError($"Gemini returned an empty or invalid model response text (GetTableSchemaFromGemini). Full response: {responseContent}");
                throw new InvalidOperationException("Gemini returned an empty or invalid response body for GetTableSchemaFromGemini.");
            }
            Logger.LogInfo($"Model Response Text (GetTableSchemaFromGemini - pre-cleaning): {modelResponseText}");

            string extractedJson = ExtractJsonBlock(modelResponseText, "GetTableSchemaFromGemini");

            // The method is expected to return a raw JSON string. Parsing and validation of this string
            // (e.g., into List<TableSchema>) is the responsibility of the caller in MainForm.cs.
            // However, we should ensure it's not empty after extraction.
            if (string.IsNullOrWhiteSpace(extractedJson))
            {
                Logger.LogError($"Extracted JSON string is empty or invalid after attempting to clean response from GetTableSchemaFromGemini. Original model response: {modelResponseText}");
                throw new InvalidOperationException("Extracted JSON string for table schema is empty or invalid after attempting to clean Gemini's response.");
            }

            Logger.LogInfo($"Returning extracted JSON for table schema (GetTableSchemaFromGemini): {extractedJson}");
            return extractedJson;
        }
    }
}