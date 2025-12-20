using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebProgramlamaFitnessCenterApp.Models;

namespace WebProgramlamaFitnessCenterApp.Services
{
    public class AIRecommendationService : IAIRecommendationService
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AIRecommendationService> _logger;

        public AIRecommendationService(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ILogger<AIRecommendationService> logger)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<string> GetExercisePlanAsync(MemberGoal profile, List<Service> services)
        {
            if (profile == null)
                return "Öneri için önce profilinizi doldurunuz.";

            var apiKey = _configuration["OpenAI:ApiKey"];
            var model = _configuration["OpenAI:Model"] ?? "gpt-4.1-mini";

            if (string.IsNullOrWhiteSpace(apiKey))
                return "OpenAI ApiKey ayarlı değil. (User-Secrets / appsettings kontrol edin)";

            var servicesText = (services == null || services.Count == 0)
                ? "Sistemde kayıtlı hizmet yok."
                : string.Join("\n", services.Select(s =>
                    $"- {s.Name} | Kategori: {s.Category} | Süre: {s.DurationMinutes} dk | Fiyat: {s.Price}"));

            var prompt = $"""
Sen bir fitness koçu gibisin. Kullanıcı profiline ve mevcut salon hizmetlerine göre kısa, net ve uygulanabilir bir plan üret.
Cevap Türkçe olsun. Maddeler halinde yaz.

Kullanıcı Profili:
- Hedef: {profile.GoalType}
- Kilo (kg): {profile.WeightKg}
- Boy (cm): {profile.HeightCm}
- Haftalık antrenman: {profile.WorkoutsPerWeek}
- Notlar: {profile.Notes}

Mevcut Hizmetler:
{servicesText}

İstenen çıktı formatı:
1) Haftalık Plan (gün gün)
2) Kardiyo / Kuvvet dağılımı
3) 3 adet güvenli beslenme önerisi
4) Dikkat edilmesi gerekenler (kısa)
""";

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromMinutes(2);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var url = "https://api.openai.com/v1/responses";

                var payload = new
                {
                    model = model,
                    input = prompt,
                    max_output_tokens = 600
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(url, content);
                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("OpenAI response not success: {Status} {Body}", response.StatusCode, responseText);
                    return "AI önerisi üretilemedi. (OpenAI isteği başarısız)";
                }

                using var doc = JsonDocument.Parse(responseText);

                if (doc.RootElement.TryGetProperty("output_text", out var outputTextEl))
                {
                    var outputText = outputTextEl.GetString();
                    return string.IsNullOrWhiteSpace(outputText) ? "AI boş çıktı döndürdü." : outputText;
                }

                var extracted = ExtractTextFallback(doc.RootElement);
                return string.IsNullOrWhiteSpace(extracted) ? "AI boş çıktı döndürdü." : extracted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AIRecommendationService hata");
                return "AI önerisi üretilemedi (hata oluştu).";
            }
        }

        private static string ExtractTextFallback(JsonElement root)
        {
            try
            {
                if (!root.TryGetProperty("output", out var outputArr) || outputArr.ValueKind != JsonValueKind.Array)
                    return "";

                var sb = new StringBuilder();
                foreach (var item in outputArr.EnumerateArray())
                {
                    if (!item.TryGetProperty("content", out var contentArr) || contentArr.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var c in contentArr.EnumerateArray())
                    {
                        if (c.TryGetProperty("text", out var textEl))
                        {
                            sb.AppendLine(textEl.GetString());
                        }
                    }
                }
                return sb.ToString().Trim();
            }
            catch
            {
                return "";
            }
        }
    }
}
