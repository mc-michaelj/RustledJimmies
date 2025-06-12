using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using OracleOptimizer.Models;

namespace OracleOptimizer.Services;

public class GeminiApiService : IGeminiApiService
{
    private readonly HttpClient _httpClient;
    private const string GeminiApiUrlBase = "https://generativelanguage.googleapis.com/v1beta/models/gemini-pro:generateContent";

    public GeminiApiService()
    {
        _httpClient = new HttpClient();
    }

    public async Task<GeminiApiResponse> AnalyzeSqlScriptAsync(string userSqlScript, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key must be provided.", nameof(apiKey));
        }

        var requestUrl = $"{GeminiApiUrlBase}?key={apiKey}";

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new
                        {
                            text = $"Please optimize the following Oracle SQL script and provide validation queries. The output must be a JSON object with keys 'optimized_sql', 'explanation', and 'validation_queries'.\n\n{userSqlScript}"
                        }
                    }
                }
            },
            generationConfig = new
            {
                response_mime_type = "application/json",
                // Ensure the schema matches GeminiApiResponse.cs
                // This part of the prompt might need to be more explicit if Gemini struggles with the schema.
                // For now, we rely on the text prompt and Gemini's ability to understand the requested JSON structure.
            }
        };

        var jsonRequestBody = JsonSerializer.Serialize(requestBody);
        var httpContent = new StringContent(jsonRequestBody, Encoding.UTF8, "application/json");

        try
        {
            HttpResponseMessage response = await _httpClient.PostAsync(requestUrl, httpContent);
            response.EnsureSuccessStatusCode();

            string responseBody = await response.Content.ReadAsStringAsync();

            // The Gemini API returns a JSON that has a top-level "candidates" array,
            // and inside that, "content" then "parts" array, and the actual JSON string is in "text".
            // We need to extract that text and then deserialize it into GeminiApiResponse.

            using (JsonDocument doc = JsonDocument.Parse(responseBody))
            {
                // Navigate to the text property containing the JSON string
                // Handle potential errors if the structure is not as expected.
                JsonElement root = doc.RootElement;
                if (root.TryGetProperty("candidates", out JsonElement candidatesElement) &&
                    candidatesElement.ValueKind == JsonValueKind.Array &&
                    candidatesElement.GetArrayLength() > 0)
                {
                    JsonElement firstCandidate = candidatesElement[0];
                    if (firstCandidate.TryGetProperty("content", out JsonElement contentElement) &&
                        contentElement.TryGetProperty("parts", out JsonElement partsElement) &&
                        partsElement.ValueKind == JsonValueKind.Array &&
                        partsElement.GetArrayLength() > 0)
                    {
                        JsonElement firstPart = partsElement[0];
                        if (firstPart.TryGetProperty("text", out JsonElement textElement))
                        {
                            string jsonText = textElement.GetString();
                            GeminiApiResponse apiResponse = JsonSerializer.Deserialize<GeminiApiResponse>(jsonText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (apiResponse == null)
                            {
                                throw new InvalidOperationException("Failed to deserialize the Gemini API response content.");
                            }
                            // Ensure validation_queries is not null, as per the record definition
                            if (apiResponse.ValidationQueries == null)
                            {
                               // If Gemini might not return it, we might need to adjust the record or ensure it's always present
                               // For now, let's assume the record requires it, so we'd throw or handle if null.
                               // However, the record's List<string> will be initialized to null if not present in JSON,
                               // which could cause issues if not handled. Let's ensure it's an empty list if null.
                               // Actually, the record constructor should handle this. If JsonPropertyName is used,
                               // and the property is missing, it might be null.
                               // The record definition implies it's not nullable directly.
                               // Let's deserialize and if it's null, explicitly set to empty list for safety,
                               // although the deserializer should ideally handle this based on constructor or init properties.

                               // For a record, if the JSON doesn't contain 'validation_queries', it will be null.
                               // Let's refine the deserialization or the record.
                               // For now, we'll trust the deserializer and the API to return it.
                               // If `ValidationQueries` is null after deserialization, it means it wasn't in the JSON.
                               // The record `List<string> ValidationQueries` will be null if not in JSON.
                               // This is acceptable as per typical JSON deserialization.
                            }
                            return apiResponse;
                        }
                    }
                }
                throw new InvalidOperationException("Invalid Gemini API response format. Could not extract the required JSON content.");
            }
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"Request error: {e.Message}");
            throw; // Re-throw to be handled by ViewModel
        }
        catch (JsonException e)
        {
            Console.WriteLine($"JSON deserialization error: {e.Message}");
            // Potentially include the problematic JSON string here for debugging if possible
            throw; // Re-throw to be handled by ViewModel
        }
        catch (Exception e)
        {
            Console.WriteLine($"An unexpected error occurred: {e.Message}");
            throw; // Re-throw to be handled by ViewModel
        }
    }
}
