using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WebProgramlamaFitnessCenterApp.Models;
using Microsoft.Extensions.Configuration;

namespace WebProgramlamaFitnessCenterApp.Services
{
    public class AIRecommendationService : IAIRecommendationService
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _http;

        public AIRecommendationService(IConfiguration config, IHttpClientFactory httpFactory)
        {
            _config = config;
            _http = httpFactory.CreateClient();
        }

        public async Task<string> GetExercisePlanAsync(MemberGoal profile, List<Service> services)
        {
            var apiKey = _config["OpenAI:ApiKey"];
            var model = _config["OpenAI:TextModel"] ?? "gpt-4.1-mini";

            // حماية: لو بالغلط موديل صور
            if (model.StartsWith("gpt-image-", StringComparison.OrdinalIgnoreCase))
                model = "gpt-4.1-mini";

            if (string.IsNullOrWhiteSpace(apiKey))
                return "AI önerisi üretilemedi: OpenAI ApiKey ayarlı değil.";

            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            var grouped = services
                .GroupBy(s => s.Category ?? "Genel")
                .Select(g => $"{g.Key}: " + string.Join(", ", g.Select(x => $"{x.Name} ({x.DurationMinutes} dk)")));

            string servicesText = string.Join(" | ", grouped);

            var workouts = profile?.WorkoutsPerWeek ?? 3;

            string profileText =
                $"Boy: {profile?.HeightCm ?? 0} cm, " +
                $"Kilo: {profile?.WeightKg ?? 0} kg, " +
                $"Hedef: {profile?.GoalType ?? "Belirsiz"}, " +
                $"Haftalık antrenman: {workouts}";

            string userPrompt = $@"
Kullanıcının bilgileri:
{profileText}

Sistemdeki mevcut hizmetler:
{servicesText}

Görev:
- Kullanıcıya 1 haftalık bir egzersiz planı hazırla.
- Planı Türkçe yaz.
- Günlere göre (Pazartesi, Salı, ...) ayır.
- Her gün için 2-3 egzersiz öner, süre veya set sayısı ile birlikte yaz.
- Mümkün olduğunda yukarıdaki hizmet isimlerini kullan (Cardio, Yoga, Pilates, Personal Training vb).
- Kullanıcı yeni başlayan ise çok ağır program verme.
";

            var requestBody = new
            {
                model = model,
                instructions = "Sen deneyimli bir fitness koçusun. Yanıtını Türkçe ver. Çıktıyı okunabilir maddeler halinde yaz.",
                input = userPrompt,
                temperature = 0.7
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync("https://api.openai.com/v1/responses", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return "AI önerisi üretilemedi. (OpenAI isteği başarısız)\n" +
                       $"Durum: {(int)response.StatusCode} {response.StatusCode}\n" +
                       $"Detay: {responseJson}";
            }

            using var doc = JsonDocument.Parse(responseJson);

            if (doc.RootElement.TryGetProperty("output_text", out var outputTextEl) &&
                outputTextEl.ValueKind == JsonValueKind.String)
            {
                var outputText = outputTextEl.GetString();
                return string.IsNullOrWhiteSpace(outputText) ? "Plan oluşturulamadı." : outputText;
            }

            // fallback parsing
            var sb = new StringBuilder();
            if (doc.RootElement.TryGetProperty("output", out var outputEl) && outputEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in outputEl.EnumerateArray())
                {
                    if (!item.TryGetProperty("content", out var contentEl) || contentEl.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var c in contentEl.EnumerateArray())
                    {
                        if (c.TryGetProperty("type", out var typeEl) &&
                            typeEl.ValueKind == JsonValueKind.String &&
                            typeEl.GetString() == "output_text" &&
                            c.TryGetProperty("text", out var textEl))
                        {
                            sb.AppendLine(textEl.GetString());
                        }
                    }
                }
            }

            var fallback = sb.ToString().Trim();
            return string.IsNullOrWhiteSpace(fallback) ? "Plan oluşturulamadı." : fallback;
        }
    }
}
