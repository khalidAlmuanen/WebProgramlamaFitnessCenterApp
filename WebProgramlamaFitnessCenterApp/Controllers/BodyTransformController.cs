using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebProgramlamaFitnessCenterApp.Data;
using WebProgramlamaFitnessCenterApp.Models;
using WebProgramlamaFitnessCenterApp.Models.ViewModels;
using WebProgramlamaFitnessCenterApp.Services;

namespace WebProgramlamaFitnessCenterApp.Controllers
{
    [Authorize]
    public class BodyTransformController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly IBodyTransformAIService _bodyAi;
        private readonly ILogger<BodyTransformController> _logger;

        public BodyTransformController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env,
            IBodyTransformAIService bodyAi,
            ILogger<BodyTransformController> logger)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
            _bodyAi = bodyAi;
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
                return View(model);

            if (string.IsNullOrWhiteSpace(model.GoalType))
            {
                ModelState.AddModelError(nameof(model.GoalType), "Lütfen bir hedef seçiniz.");
                return View(model);
            }

            if (!model.StartWeightKg.HasValue)
            {
                ModelState.AddModelError(nameof(model.StartWeightKg), "Lütfen başlangıç kilonuzu giriniz.");
                return View(model);
            }

            if (model.ImageFile == null || model.ImageFile.Length == 0)
            {
                ModelState.AddModelError(nameof(model.ImageFile), "Lütfen bir fotoğraf yükleyiniz.");
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            // Save original image
            var originalFolder = Path.Combine(_env.WebRootPath, "uploads", "original");
            Directory.CreateDirectory(originalFolder);

            var originalFileName = Guid.NewGuid() + Path.GetExtension(model.ImageFile.FileName);
            var originalPhysicalPath = Path.Combine(originalFolder, originalFileName);

            using (var stream = new FileStream(originalPhysicalPath, FileMode.Create))
            {
                await model.ImageFile.CopyToAsync(stream);
            }

            var originalRelativePath = "/uploads/original/" + originalFileName;

            // Generate with AI
            string generatedRelativePath = "";
            double expectedPercent = 0;

            try
            {
                var result = await _bodyAi.GenerateTransformedImageAsync(
                    originalImagePath: originalPhysicalPath,
                    goalType: model.GoalType,
                    durationMonths: model.DurationMonths,
                    startWeightKg: model.StartWeightKg.Value
                );

                generatedRelativePath = result.generatedImagePath ?? "";
                expectedPercent = result.expectedChangePercent;

                if (string.IsNullOrWhiteSpace(generatedRelativePath))
                    TempData["HataMsj"] = "Dönüşüm görseli oluşturulamadı (ApiKey/Billing/Limit).";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BodyTransform AI error");
                TempData["HataMsj"] = "Yapay zekâ hata verdi. ApiKey/Billing kontrol edin.";
            }

            //  Save DB
            var request = new BodyTransformRequest
            {
                MemberId = user.Id,
                GoalType = model.GoalType,
                DurationMonths = model.DurationMonths,
                StartWeightKg = model.StartWeightKg,
                ExpectedChangePercent = expectedPercent,
                OriginalImagePath = originalRelativePath,
                GeneratedImagePath = generatedRelativePath,
                CreatedAt = DateTime.UtcNow
            };

            _context.BodyTransformRequests.Add(request);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(request.GeneratedImagePath))
                TempData["Msj"] = "Dönüşüm görseli başarıyla oluşturuldu.";

            var redirectUrl = Url.Action(nameof(Result), "BodyTransform", new { id = request.Id }) ?? $"/BodyTransform/Result/{request.Id}";

            var isAjax =
                Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                (Request.Headers.TryGetValue("Accept", out var accept) && accept.ToString().Contains("application/json"));

            if (isAjax)
                return Ok(new { redirectUrl });

            return Redirect(redirectUrl);
        }

        [HttpGet]
        public async Task<IActionResult> Result(int id)
        {
            var req = await _context.BodyTransformRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (req == null)
                return NotFound();

            return View(req);
        }

        [HttpGet]
        public async Task<IActionResult> MyRequests()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var list = await _context.BodyTransformRequests
                .AsNoTracking()
                .Where(x => x.MemberId == user.Id)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return View(list);
        }
    }
}
