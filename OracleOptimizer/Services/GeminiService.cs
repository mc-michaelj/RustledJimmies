using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace OracleOptimizer.Services
{
    /// <summary>
    /// Represents the structured response expected from the Gemini API when analyzing a SQL procedure.
    /// </summary>
    public class GeminiResponse
    {
        /// <summary>
        /// Gets or sets the SQL SELECT statement to capture the data state *before* the main procedure logic is executed.
        /// This query is used for validating the procedure's impact.
        /// </summary>
        [JsonProperty("validation_query_before")]
        public string? ValidationQueryBefore { get; set; }

        /// <summary>
        /// Gets or sets the full text of the optimized PL/SQL procedure body.
        /// </summary>
        [JsonProperty("optimized_procedure_body")]
        public string? OptimizedProcedureBody { get; set; }

        /// <summary>
        /// Gets or sets the SQL SELECT statement to capture the data state *after* the main procedure logic has been executed.
        /// This query is used for validating the procedure's impact by comparing its results with those of ValidationQueryBefore.
        /// </summary>
        [JsonProperty("validation_query_after")]
        public string? ValidationQueryAfter { get; set; }

        /// <summary>
        /// Gets or sets a detailed explanation of the optimizations applied to the procedure and the overall validation plan.
        /// </summary>
        [JsonProperty("explanation")]
        public string? Explanation { get; set; }
    }

    /// <summary>
    /// Service class to interact with the Google Gemini API for SQL analysis and optimization tasks.
    /// </summary>
    public class GeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="GeminiService"/> class with the specified API key.
        /// </summary>
        /// <param name="apiKey">The Google Gemini API key.</param>
        public GeminiService(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            // Set default headers for the HttpClient instance.
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Analyzes an Oracle PL/SQL procedure body using the Gemini API to get an optimized version and validation queries.
        /// </summary>
        /// <param name="procedureBody">The PL/SQL procedure body text to analyze.</param>
        /// <param name="modelName">The specific Gemini model to use for the analysis (e.g., "gemini-2.5-flash-preview-05-20").</param>
        /// <returns>A <see cref="Task{GeminiResponse}"/> representing the asynchronous operation.
        /// The task result contains a <see cref="GeminiResponse"/> object if successful, otherwise null or an exception is thrown.</returns>
        /// <exception cref="HttpRequestException">Thrown if the API call fails with an unsuccessful status code.</exception>
        /// <exception cref="Exception">Thrown if the API returns an empty or invalid response, or if a JSON block cannot be found.</exception>
        /// <exception cref="JsonReaderException">Thrown if parsing the extracted JSON block fails.</exception>
        public async Task<GeminiResponse?> AnalyzeSqlAsync(string procedureBody, string modelName)
        {
            // Master prompt defining the role and expected output format for the Gemini API.
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
            // Construct the request body with the user's procedure.
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

            // Serialize the request body to JSON and create StringContent.
            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            // Make the POST request to the Gemini API.
            var response = await _httpClient.PostAsync($"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={_apiKey}", content);

            // Log the request and response details for debugging.
            System.Diagnostics.Debug.WriteLine($"Request URL: https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={_apiKey}");
            System.Diagnostics.Debug.WriteLine($"Request Body: {JsonConvert.SerializeObject(requestBody)}");
            System.Diagnostics.Debug.WriteLine($"Response Status: {response.StatusCode}");
            var responseContent = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"Response Content: {responseContent}");

            // Handle unsuccessful API responses.
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Error calling Gemini API: {response.StatusCode}. Details: {responseContent}");
            }

            // Deserialize the overall API response to access the nested content.
            // The actual model-generated text is typically found within a nested structure (e.g., candidates[0].content.parts[0].text).
            dynamic? parsedJson = JsonConvert.DeserializeObject<dynamic>(responseContent);
            string? modelResponseText = parsedJson?.candidates[0]?.content?.parts[0]?.text?.ToString();

            // Check if the extracted model response text is empty.
            if (string.IsNullOrWhiteSpace(modelResponseText))
            {
                throw new Exception("Gemini returned an empty or invalid response body.");
            }

            // The AI might wrap the JSON in markdown (```json ... ```) or include conversational text.
            // This section attempts to extract the clean JSON block.
            string jsonBlock = modelResponseText;
            string jsonMarker = "```json";
            int startIndex = modelResponseText.IndexOf(jsonMarker);

            if (startIndex != -1) // If the ```json marker is found
            {
                startIndex += jsonMarker.Length; // Move past the marker
                int endIndex = modelResponseText.LastIndexOf("```"); // Find the closing markdown marker
                if (endIndex > startIndex)
                {
                    jsonBlock = modelResponseText.Substring(startIndex, endIndex - startIndex).Trim();
                }
                // If no closing ``` is found, it might be an incomplete response or the JSON is the rest of the string.
                // For simplicity, we might assume the rest of the string is the JSON, or handle error.
                // Current logic implicitly uses the rest of the string if endIndex is not found or not after startIndex.
            }
            else
            {
                // Fallback strategy: If no markdown block is explicitly found,
                // try to find the first opening curly brace '{' and the last closing curly brace '}'.
                // This assumes the main JSON content is the first complete JSON object in the response.
                int firstBrace = modelResponseText.IndexOf('{');
                int lastBrace = modelResponseText.LastIndexOf('}');

                if (firstBrace != -1 && lastBrace > firstBrace)
                {
                    jsonBlock = modelResponseText.Substring(firstBrace, lastBrace - firstBrace + 1);
                }
                else
                {
                    // If no clear JSON object is found by braces either, the response is considered invalid.
                    throw new Exception("Could not find a valid JSON object in the Gemini response.");
                }
            }

            // Attempt to deserialize the extracted JSON block into the GeminiResponse object.
            try
            {
                return JsonConvert.DeserializeObject<GeminiResponse>(jsonBlock);
            }
            catch (JsonReaderException ex)
            {
                // If deserialization fails, throw a more specific exception including the problematic JSON block
                // to aid in debugging issues with the Gemini API's output format.
                throw new JsonReaderException($"Failed to parse the following JSON block from Gemini: {jsonBlock}", ex);
            }
        }

        /// <summary>
        /// Retrieves a JSON string describing the table schemas (table names, column names, and data types)
        /// inferred from the provided SQL script using the Gemini API.
        /// </summary>
        /// <param name="sqlScript">The SQL script to analyze for table schemas.</param>
        /// <param name="modelName">The specific Gemini model to use (e.g., "gemini-2.5-flash-preview-05-20").</param>
        /// <returns>A <see cref="Task{String}"/> representing the asynchronous operation.
        /// The task result contains a JSON string detailing the table schemas if successful.
        /// This JSON string is expected to be an array of objects, each with 'tableName' and 'columns' (with 'columnName', 'dataType').</returns>
        /// <exception cref="HttpRequestException">Thrown if the API call fails with an unsuccessful status code.</exception>
        /// <exception cref="Exception">Thrown if the API returns an empty or invalid response, or if the extracted JSON is empty.</exception>
        public async Task<string> GetTableSchemaFromGemini(string sqlScript, string modelName)
        {
            // Prompt instructing Gemini to return a JSON object describing table structures.
            // Emphasizes the need for fully qualified table names.
            string prompt = @"Based on the following SQL script, return a JSON object describing the tables involved. 
The JSON must be an array where each object has 'tableName' and an array of 'columns' with 'columnName' and 'dataType' (use Oracle types like VARCHAR2, NUMBER, DATE).
For each 'tableName', you MUST provide the fully qualified name, including the schema (e.g., 'MYSCHEMA.MYTABLE'). If a table is used without a schema in the script, you must still identify and include its correct schema in the response.
This is critical to avoid 'table not found' errors during testing.
SQL Script: {sqlScript}";

            // Substitute the placeholder with the actual SQL script.
            prompt = prompt.Replace("{sqlScript}", sqlScript);

            // Construct the request body.
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

            // Serialize request and create StringContent.
            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            // Make the POST request.
            var response = await _httpClient.PostAsync($"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={_apiKey}", content);

            // Log request and response details.
            System.Diagnostics.Debug.WriteLine($"Request URL (GetTableSchemaFromGemini): https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={_apiKey}");
            System.Diagnostics.Debug.WriteLine($"Request Body (GetTableSchemaFromGemini): {JsonConvert.SerializeObject(requestBody)}");
            System.Diagnostics.Debug.WriteLine($"Response Status (GetTableSchemaFromGemini): {response.StatusCode}");
            var responseContent = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"Response Content (GetTableSchemaFromGemini): {responseContent}");

            // Handle unsuccessful API responses.
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Error calling Gemini API (GetTableSchemaFromGemini): {response.StatusCode}. Details: {responseContent}");
            }

            // Deserialize the overall API response.
            dynamic? parsedJson = JsonConvert.DeserializeObject<dynamic>(responseContent);
            // Extract the model's response text.
            string? modelResponseText = parsedJson?.candidates[0]?.content?.parts[0]?.text?.ToString();

            // Check for empty model response.
            if (string.IsNullOrWhiteSpace(modelResponseText))
            {
                throw new Exception("Gemini returned an empty or invalid response for GetTableSchemaFromGemini.");
            }

            // Attempt to clean the response:
            // The API is expected to return a direct JSON string for this prompt.
            // However, it might still be wrapped in markdown ```json ... ```.
            string jsonResult = modelResponseText.Trim();
            string jsonMarker = "```json";
            if (jsonResult.StartsWith(jsonMarker)) // Check if response starts with ```json
            {
                jsonResult = jsonResult.Substring(jsonMarker.Length); // Remove ```json marker
                if (jsonResult.EndsWith("```")) // Check if it also ends with ```
                {
                    jsonResult = jsonResult.Substring(0, jsonResult.Length - 3); // Remove trailing ```
                }
                jsonResult = jsonResult.Trim(); // Trim any whitespace
            }
            // Additional check: if not markdown, verify if it looks like a JSON object or array.
            else if (jsonResult.StartsWith("{") && jsonResult.EndsWith("}") || jsonResult.StartsWith("[") && jsonResult.EndsWith("]"))
            {
                // String appears to be plain JSON (object or array), no further stripping needed.
            }
            else
            {
                // If the response is not wrapped in markdown and doesn't start/end with typical JSON braces/brackets,
                // it might be an error message from Gemini or an unexpected format.
                // Log this as a warning, as the downstream parsing might fail.
                System.Diagnostics.Debug.WriteLine($"Warning (GetTableSchemaFromGemini): Response doesn't appear to be clean JSON or markdown-wrapped JSON: {jsonResult}");
            }

            // Final check if the processed JSON string is empty.
            if (string.IsNullOrWhiteSpace(jsonResult))
            {
                throw new Exception("Extracted JSON string is empty or invalid after attempting to clean response from GetTableSchemaFromGemini.");
            }

            // Return the (hopefully) clean JSON string.
            return jsonResult;
        }
    }
}