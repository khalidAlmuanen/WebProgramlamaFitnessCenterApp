using System.Linq;
using System.Threading.Tasks;
using WebProgramlamaFitnessCenterApp.Data;          
using WebProgramlamaFitnessCenterApp.Models;         
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WebProgramlamaFitnessCenterApp.Controllers     
{
    [Authorize]
    public class GymController : Controller
    {
        private readonly ApplicationDbContext _context;

        public GymController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var gyms = await _context.Gyms
                .OrderBy(g => g.Name)
                .ToListAsync();

            return View(gyms);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var gym = await _context.Gyms
                .Include(g => g.Services)
                .Include(g => g.Trainers)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (gym == null) return NotFound();

            return View(gym);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Gym gym)
        {
            // Aynı isimde gym var mı? (DB index zaten var, ama user-friendly olsun)
            bool exists = await _context.Gyms.AnyAsync(g => g.Name == gym.Name);
            if (exists)
            {
                ModelState.AddModelError(string.Empty, "Bu isimde bir spor salonu zaten kayıtlı.");
            }

            if (!ModelState.IsValid)
                return View(gym);

            _context.Gyms.Add(gym);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Spor salonu başarıyla eklendi.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var gym = await _context.Gyms.FindAsync(id);
            if (gym == null) return NotFound();

            return View(gym);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Gym gym)
        {
            if (id != gym.Id) return NotFound();

            bool exists = await _context.Gyms.AnyAsync(g => g.Id != gym.Id && g.Name == gym.Name);
            if (exists)
            {
                ModelState.AddModelError(string.Empty, "Bu isimde başka bir spor salonu zaten kayıtlı.");
            }

            if (!ModelState.IsValid)
                return View(gym);

            try
            {
                _context.Update(gym);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Spor salonu başarıyla güncellendi.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Gyms.AnyAsync(g => g.Id == gym.Id))
                    return NotFound();

                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var gym = await _context.Gyms
                .FirstOrDefaultAsync(g => g.Id == id);

            if (gym == null) return NotFound();

            // Bilgilendirme: bağlı kayıt var mı?
            ViewBag.HasServices = await _context.Services.AnyAsync(s => s.GymId == gym.Id);
            ViewBag.HasTrainers = await _context.Trainers.AnyAsync(t => t.GymId == gym.Id);

            // Eğer Appointments Gym'e direkt bağlı değilse, Trainer/Service üzerinden kontrol:
            var serviceIds = await _context.Services
                .Where(s => s.GymId == gym.Id)
                .Select(s => s.Id)
                .ToListAsync();

            var trainerIds = await _context.Trainers
                .Where(t => t.GymId == gym.Id)
                .Select(t => t.Id)
                .ToListAsync();

            bool hasAppointments = await _context.Appointments.AnyAsync(a =>
                serviceIds.Contains(a.ServiceId) || trainerIds.Contains(a.TrainerId));

            ViewBag.HasAppointments = hasAppointments;

            return View(gym);
        }

        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var gym = await _context.Gyms.FindAsync(id);
            if (gym == null)
            {
                TempData["Error"] = "Silinecek spor salonu bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            bool hasServices = await _context.Services.AnyAsync(s => s.GymId == id);
            if (hasServices)
            {
                TempData["Error"] = "Bu spor salonuna bağlı hizmetler olduğu için silinemez.";
                return RedirectToAction(nameof(Index));
            }

            bool hasTrainers = await _context.Trainers.AnyAsync(t => t.GymId == id);
            if (hasTrainers)
            {
                TempData["Error"] = "Bu spor salonuna bağlı antrenörler olduğu için silinemez.";
                return RedirectToAction(nameof(Index));
            }

            var serviceIds = await _context.Services
                .Where(s => s.GymId == id)
                .Select(s => s.Id)
                .ToListAsync();

            var trainerIds = await _context.Trainers
                .Where(t => t.GymId == id)
                .Select(t => t.Id)
                .ToListAsync();

            bool hasAppointments = await _context.Appointments.AnyAsync(a =>
                serviceIds.Contains(a.ServiceId) || trainerIds.Contains(a.TrainerId));

            if (hasAppointments)
            {
                TempData["Error"] = "Bu spor salonuna bağlı randevular olduğu için silinemez.";
                return RedirectToAction(nameof(Index));
            }

            _context.Gyms.Remove(gym);

            try
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = "Spor salonu başarıyla silindi.";
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "Silme işlemi sırasında veritabanı hatası oluştu.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
