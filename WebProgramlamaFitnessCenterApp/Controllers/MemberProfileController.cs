using System.Threading.Tasks;
using WebProgramlamaFitnessCenterApp.Data;
using WebProgramlamaFitnessCenterApp.Models;
using WebProgramlamaFitnessCenterApp.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WebProgramlamaFitnessCenterApp.Controllers
{
    [Authorize]
    public class MemberProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public MemberProfileController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // GET: /MemberProfile/Index
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var goal = await _context.MemberGoals
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.UserId == user.Id);

            var vm = new MemberProfileViewModel
            {
                HeightCm        = goal?.HeightCm,
                WeightKg        = goal?.WeightKg,
                GoalType        = goal?.GoalType,
                WorkoutsPerWeek = goal?.WorkoutsPerWeek,
                Notes           = goal?.Notes
            };

            return View(vm);
        }

        // POST: /MemberProfile/Index
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(MemberProfileViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var goal = await _context.MemberGoals
                .FirstOrDefaultAsync(m => m.UserId == user.Id);

            if (goal == null)
            {
                goal = new MemberGoal
                {
                    UserId          = user.Id,
                    HeightCm        = model.HeightCm,
                    WeightKg        = model.WeightKg,
                    GoalType        = model.GoalType,
                    WorkoutsPerWeek = model.WorkoutsPerWeek,
                    Notes           = model.Notes
                };

                _context.MemberGoals.Add(goal);
            }
            else
            {
                goal.HeightCm        = model.HeightCm;
                goal.WeightKg        = model.WeightKg;
                goal.GoalType        = model.GoalType;
                goal.WorkoutsPerWeek = model.WorkoutsPerWeek;
                goal.Notes           = model.Notes;
            }

            await _context.SaveChangesAsync();

            // Senin mevcut anahtarın (bozulmasın)
            TempData["ProfileSaved"] = "Profil bilgileriniz başarıyla güncellendi.";

            // Hocanın tarzı (derslerdeki Msj)
            TempData["Msj"] = "Profil bilgileriniz başarıyla güncellendi.";

            return RedirectToAction(nameof(Index));
        }
    }
}
