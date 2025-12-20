using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WebProgramlamaFitnessCenterApp.Services
{
    public class OpenAIBodyTransformAIService : IBodyTransformAIService
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<OpenAIBodyTransformAIService> _logger;

        public OpenAIBodyTransformAIService(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            IWebHostEnvironment env,
            ILogger<OpenAIBodyTransformAIService> logger)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _env = env;
            _logger = logger;
        }

        public async Task<(string generatedImagePath, double expectedChangePercent)> GenerateTransformedImageAsync(
            string originalImagePath,
            string goalType,
            int durationMonths,
            double startWeightKg)
        {
            var expectedChangePercent = CalculateExpectedChangePercent(goalType, durationMonths);

            var apiKey = _configuration["OpenAI:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return (string.Empty, expectedChangePercent);
            }

            try
            {
                var prompt = BuildImagePrompt(goalType, durationMonths, expectedChangePercent);

                var client = _httpClientFactory.CreateClient();

                using var form = new MultipartFormDataContent();

                var imageBytes = await File.ReadAllBytesAsync(originalImagePath);
                var imageContent = new ByteArrayContent(imageBytes);
                imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
                form.Add(imageContent, "image", "input.png");

                form.Add(new StringContent("gpt-image-1"), "model");
                form.Add(new StringContent(prompt), "prompt");
                form.Add(new StringContent("1"), "n");
                form.Add(new StringContent("1024x1024"), "size");

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/images/edits");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = form;

                var response = await client.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("OpenAI image edit failed: {Status} {Body}", response.StatusCode, body);
                    return (string.Empty, expectedChangePercent);
                }

                var imageB64 = ParseBase64FromResponse(body);
                if (string.IsNullOrWhiteSpace(imageB64))
                    return (string.Empty, expectedChangePercent);

                var outputRelative = await SaveBase64ToWwwRootAsync(imageB64);
                return (outputRelative, expectedChangePercent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenAIBodyTransformAIService error");
                return (string.Empty, expectedChangePercent);
            }
        }

        private static double CalculateExpectedChangePercent(string goalType, int durationMonths)
        {
            goalType = (goalType ?? "").ToLowerInvariant();

            double basePerMonth =
                goalType.Contains("kilo") ? -1.5 :
                goalType.Contains("kas") ? 1.0 :
                -0.3;

            var pct = basePerMonth * Math.Clamp(durationMonths, 1, 24);
            return Math.Round(pct, 2);
        }

        private static string BuildImagePrompt(string goalType, int durationMonths, double expectedChangePercent)
        {
            return
$@"A realistic fitness body transformation preview.
Goal: {goalType}
Duration: {durationMonths} months
Expected change: {expectedChangePercent}%.
Keep the same person identity, same clothing style, realistic lighting, natural proportions, no exaggeration.";
        }

        private static string? ParseBase64FromResponse(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var data = doc.RootElement.GetProperty("data");
                if (data.GetArrayLength() == 0) return null;
                return data[0].GetProperty("b64_json").GetString();
            }
            catch
            {
                return null;
            }
        }

        private async Task<string> SaveBase64ToWwwRootAsync(string base64)
        {
            var bytes = Convert.FromBase64String(base64);

            var folder = Path.Combine(_env.WebRootPath, "uploads", "generated");
            Directory.CreateDirectory(folder);

            var fileName = Guid.NewGuid() + ".png";
            var fullPath = Path.Combine(folder, fileName);

            await File.WriteAllBytesAsync(fullPath, bytes);

            var relative = "/uploads/generated/" + fileName;
            _logger.LogInformation("Generated image saved at {Path}", relative);
            return relative;
        }
    }
}
