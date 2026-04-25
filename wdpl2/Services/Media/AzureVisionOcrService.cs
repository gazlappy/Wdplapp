using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wdpl2.Services;

/// <summary>
/// Azure Computer Vision OCR service with excellent handwriting recognition.
/// Uses the Read API (v3.2) which is specifically designed for handwriting and printed text.
/// 
/// To use this service:
/// 1. Create an Azure Computer Vision resource at https://portal.azure.com
/// 2. Get your endpoint and key from the resource's "Keys and Endpoint" section
/// 3. Configure the endpoint and key in settings
/// 
/// Pricing: ~$1.50 per 1000 images (S1 tier) - very affordable for occasional use
/// Free tier: 20 calls per minute, 5000 calls per month
/// </summary>
public class AzureVisionOcrService
{
    private readonly HttpClient _httpClient;
    private string _endpoint = "";
    private string _apiKey = "";
    
    public bool IsConfigured => !string.IsNullOrEmpty(_endpoint) && !string.IsNullOrEmpty(_apiKey);

    public AzureVisionOcrService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        // Load saved configuration
        LoadConfiguration();
    }

    public void Configure(string endpoint, string apiKey)
    {
        _endpoint = endpoint.TrimEnd('/');
        _apiKey = apiKey;
        
        // Save configuration
        SaveConfiguration();
    }

    private void LoadConfiguration()
    {
        try
        {
            _endpoint = Preferences.Get("AzureVision_Endpoint", "");
            _apiKey = Preferences.Get("AzureVision_ApiKey", "");
        }
        catch
        {
            // Ignore errors loading preferences
        }
    }

    private void SaveConfiguration()
    {
        try
        {
            Preferences.Set("AzureVision_Endpoint", _endpoint);
            Preferences.Set("AzureVision_ApiKey", _apiKey);
        }
        catch
        {
            // Ignore errors saving preferences
        }
    }

    public void ClearConfiguration()
    {
        _endpoint = "";
        _apiKey = "";
        try
        {
            Preferences.Remove("AzureVision_Endpoint");
            Preferences.Remove("AzureVision_ApiKey");
        }
        catch { }
    }

    /// <summary>
    /// Recognize text (including handwriting) from an image using Azure Computer Vision Read API
    /// </summary>
    public async Task<AzureOcrResult> RecognizeTextAsync(byte[] imageData, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new AzureOcrResult
            {
                Success = false,
                Error = "Azure Vision is not configured. Please add your Azure endpoint and API key in settings."
            };
        }

        try
        {
            // Step 1: Submit the image to the Read API
            var readUrl = $"{_endpoint}/vision/v3.2/read/analyze";
            
            using var content = new ByteArrayContent(imageData);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            
            using var request = new HttpRequestMessage(HttpMethod.Post, readUrl);
            request.Headers.Add("Ocp-Apim-Subscription-Key", _apiKey);
            request.Content = content;

            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                return new AzureOcrResult
                {
                    Success = false,
                    Error = $"Azure API error ({response.StatusCode}): {errorBody}"
                };
            }

            // Step 2: Get the operation location from the response header
            if (!response.Headers.TryGetValues("Operation-Location", out var operationLocations))
            {
                return new AzureOcrResult
                {
                    Success = false,
                    Error = "No Operation-Location header in response"
                };
            }

            var operationLocation = operationLocations.First();

            // Step 3: Poll for the result
            var result = await PollForResultAsync(operationLocation, cancellationToken);
            return result;
        }
        catch (TaskCanceledException)
        {
            return new AzureOcrResult
            {
                Success = false,
                Error = "Request was cancelled"
            };
        }
        catch (HttpRequestException ex)
        {
            return new AzureOcrResult
            {
                Success = false,
                Error = $"Network error: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new AzureOcrResult
            {
                Success = false,
                Error = $"Unexpected error: {ex.Message}"
            };
        }
    }

    private async Task<AzureOcrResult> PollForResultAsync(string operationLocation, CancellationToken cancellationToken)
    {
        const int maxRetries = 30; // 30 seconds max wait
        const int delayMs = 1000;  // 1 second between polls

        for (int i = 0; i < maxRetries; i++)
        {
            await Task.Delay(delayMs, cancellationToken);

            using var request = new HttpRequestMessage(HttpMethod.Get, operationLocation);
            request.Headers.Add("Ocp-Apim-Subscription-Key", _apiKey);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                continue; // Retry on error
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var readResult = JsonSerializer.Deserialize<AzureReadResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (readResult == null)
            {
                continue;
            }

            switch (readResult.Status?.ToLower())
            {
                case "succeeded":
                    return ParseReadResult(readResult);
                    
                case "failed":
                    return new AzureOcrResult
                    {
                        Success = false,
                        Error = "Azure Read API failed to process the image"
                    };
                    
                case "running":
                case "notstarted":
                    // Continue polling
                    break;
                    
                default:
                    // Unknown status, continue polling
                    break;
            }
        }

        return new AzureOcrResult
        {
            Success = false,
            Error = "Timeout waiting for Azure to process the image"
        };
    }

    private AzureOcrResult ParseReadResult(AzureReadResponse response)
    {
        var result = new AzureOcrResult
        {
            Success = true,
            Lines = new List<OcrLine>(),
            Words = new List<OcrWord>()
        };

        var allText = new StringBuilder();
        var readResults = response.AnalyzeResult?.ReadResults ?? new List<ReadResult>();

        foreach (var page in readResults)
        {
            result.Width = page.Width;
            result.Height = page.Height;

            foreach (var line in page.Lines ?? new List<LineResult>())
            {
                var ocrLine = new OcrLine
                {
                    Text = line.Text ?? "",
                    BoundingBox = line.BoundingBox ?? Array.Empty<double>(),
                    Confidence = 1.0 // Azure doesn't provide line-level confidence
                };

                // Parse words
                foreach (var word in line.Words ?? new List<WordResult>())
                {
                    var ocrWord = new OcrWord
                    {
                        Text = word.Text ?? "",
                        BoundingBox = word.BoundingBox ?? Array.Empty<double>(),
                        Confidence = word.Confidence
                    };
                    ocrLine.Words.Add(ocrWord);
                    result.Words.Add(ocrWord);
                }

                result.Lines.Add(ocrLine);
                allText.AppendLine(line.Text);
            }
        }

        result.AllText = allText.ToString().Trim();
        
        // Calculate overall confidence
        if (result.Words.Count > 0)
        {
            result.AverageConfidence = result.Words.Average(w => w.Confidence);
        }

        return result;
    }

    /// <summary>
    /// Test the Azure connection with a simple API call
    /// </summary>
    public async Task<(bool Success, string Message)> TestConnectionAsync()
    {
        if (!IsConfigured)
        {
            return (false, "Not configured - please add endpoint and API key");
        }

        try
        {
            // Use a simple health check endpoint
            var url = $"{_endpoint}/vision/v3.2/read/analyze";
            
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Ocp-Apim-Subscription-Key", _apiKey);
            
            // Send empty content to test auth
            request.Content = new StringContent("", Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            
            // We expect a 400 (bad request) because we didn't send an image, but this confirms auth works
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                return (true, "Connection successful - Azure Vision API is accessible");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return (false, "Authentication failed - check your API key");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return (false, "Endpoint not found - check your endpoint URL");
            }
            else
            {
                return (false, $"Unexpected response: {response.StatusCode}");
            }
        }
        catch (HttpRequestException ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, $"Error: {ex.Message}");
        }
    }
}

#region Azure API Response Models

public class AzureOcrResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string AllText { get; set; } = "";
    public List<OcrLine> Lines { get; set; } = new();
    public List<OcrWord> Words { get; set; } = new();
    public double AverageConfidence { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

public class OcrLine
{
    public string Text { get; set; } = "";
    public double[] BoundingBox { get; set; } = Array.Empty<double>();
    public double Confidence { get; set; }
    public List<OcrWord> Words { get; set; } = new();
}

public class OcrWord
{
    public string Text { get; set; } = "";
    public double[] BoundingBox { get; set; } = Array.Empty<double>();
    public double Confidence { get; set; }
}

// Azure Read API Response Models
public class AzureReadResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }
    
    [JsonPropertyName("analyzeResult")]
    public AnalyzeResult? AnalyzeResult { get; set; }
}

public class AnalyzeResult
{
    [JsonPropertyName("readResults")]
    public List<ReadResult>? ReadResults { get; set; }
}

public class ReadResult
{
    [JsonPropertyName("page")]
    public int Page { get; set; }
    
    [JsonPropertyName("width")]
    public double Width { get; set; }
    
    [JsonPropertyName("height")]
    public double Height { get; set; }
    
    [JsonPropertyName("unit")]
    public string? Unit { get; set; }
    
    [JsonPropertyName("lines")]
    public List<LineResult>? Lines { get; set; }
}

public class LineResult
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
    
    [JsonPropertyName("boundingBox")]
    public double[]? BoundingBox { get; set; }
    
    [JsonPropertyName("words")]
    public List<WordResult>? Words { get; set; }
}

public class WordResult
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
    
    [JsonPropertyName("boundingBox")]
    public double[]? BoundingBox { get; set; }
    
    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}

#endregion
