using System.Threading.Tasks;

namespace WebProgramlamaFitnessCenterApp.Services
{
    public interface IBodyTransformAIService
    {
        Task<(string generatedImagePath, double expectedChangePercent)> GenerateTransformedImageAsync(
            string originalImagePath,
            string goalType,
            int durationMonths,
            double startWeightKg);
    }
}
