using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WebProgramlamaFitnessCenterApp.Data;
using WebProgramlamaFitnessCenterApp.Models;
using WebProgramlamaFitnessCenterApp.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace WebProgramlamaFitnessCenterApp.Controllers
{
    [Authorize]
    public class BodyTransformController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<BodyTransformController> _logger;

        public BodyTransformController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env,
            ILogger<BodyTransformController> logger)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Create()
        {
            var vm = new BodyTransformCreateViewModel
            {
                DurationMonths = 12
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BodyTransformCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.ImageFile == null || model.ImageFile.Length == 0)
            {
                ModelState.AddModelError("ImageFile", "Lütfen bir fotoğraf yükleyiniz.");
                return View(model);
            }

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "original");
            Directory.CreateDirectory(uploadsFolder);

            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.ImageFile.FileName);
            var physicalPath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(physicalPath, FileMode.Create))
            {
                await model.ImageFile.CopyToAsync(stream);
            }

            var relativeOriginalPath = "/uploads/original/" + fileName;
            _logger.LogInformation("Original image saved successfully at {Path}", physicalPath);

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var request = new BodyTransformRequest
            {
                MemberId = user.Id,
                GoalType = model.GoalType,
                DurationMonths = model.DurationMonths,
                StartWeightKg = model.StartWeightKg,
                OriginalImagePath = relativeOriginalPath,
                CreatedAt = DateTime.UtcNow
            };

            _context.BodyTransformRequests.Add(request);
            await _context.SaveChangesAsync();

            return RedirectToAction("Result", new { id = request.Id });
        }

        public async Task<IActionResult> Result(int id)
        {
            var request = await _context.BodyTransformRequests
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
                return NotFound();

            return View(request);
        }

        public async Task<IActionResult> MyRequests()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var list = await _context.BodyTransformRequests
                .Where(b => b.MemberId == user.Id)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return View(list);
        }
    }
}
