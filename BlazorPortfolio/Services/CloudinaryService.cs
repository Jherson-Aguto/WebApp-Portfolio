using System.Net.Http.Headers;
using System.Text.Json;

namespace BlazorPortfolio.Services;

public class CloudinaryService(IConfiguration config, ILogger<CloudinaryService> logger)
{
    private readonly string? _cloudName   = config["Cloudinary:CloudName"];
    private readonly string? _apiKey      = config["Cloudinary:ApiKey"];
    private readonly string? _apiSecret   = config["Cloudinary:ApiSecret"];

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_cloudName) &&
        !string.IsNullOrWhiteSpace(_apiKey)    &&
        !string.IsNullOrWhiteSpace(_apiSecret);

    /// <summary>
    /// Uploads a stream to Cloudinary and returns the secure public URL.
    /// </summary>
    public async Task<string> UploadAsync(Stream fileStream, string fileName)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Cloudinary is not configured. Set Cloudinary:CloudName, ApiKey, and ApiSecret.");

        // Cloudinary signed upload: timestamp + SHA-1 signature
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var folder    = "portfolio";
        var toSign    = $"folder={folder}&timestamp={timestamp}{_apiSecret}";
        var signature = ComputeSha1(toSign);

        using var http    = new HttpClient();
        using var content = new MultipartFormDataContent();

        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(GetMimeType(fileName));
        content.Add(fileContent,    "file", fileName);
        content.Add(new StringContent(_apiKey!),   "api_key");
        content.Add(new StringContent(timestamp),  "timestamp");
        content.Add(new StringContent(signature),  "signature");
        content.Add(new StringContent(folder),     "folder");

        var url      = $"https://api.cloudinary.com/v1_1/{_cloudName}/image/upload";
        var response = await http.PostAsync(url, content);
        var body     = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Cloudinary upload failed: {Body}", body);
            throw new Exception($"Cloudinary upload failed: {response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("secure_url").GetString()
               ?? throw new Exception("Cloudinary response missing secure_url");
    }

    private static string ComputeSha1(string input)
    {
        var bytes = System.Security.Cryptography.SHA1.HashData(
            System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GetMimeType(string fileName) => Path.GetExtension(fileName).ToLower() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png"            => "image/png",
        ".webp"           => "image/webp",
        _                 => "application/octet-stream"
    };
}
