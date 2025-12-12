using System.Collections.Generic;
using System.Threading.Tasks;
using WebProgramlamaFitnessCenterApp.Models;

namespace WebProgramlamaFitnessCenterApp.Services
{
    public interface IAIRecommendationService
    {
        Task<string> GetExercisePlanAsync(MemberGoal profile, List<Service> services);
    }
}
