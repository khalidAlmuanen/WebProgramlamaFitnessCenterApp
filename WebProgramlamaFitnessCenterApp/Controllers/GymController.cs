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
            var gyms = await _context.Gyms.ToListAsync();
            return View(gyms);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(Gym gym)
        {
            if (!ModelState.IsValid)
                return View(gym);

            _context.Gyms.Add(gym);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Spor salonu başarıyla eklendi.";
            TempData["Msj"] = "Spor salonu başarıyla eklendi.";

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
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, Gym gym)
        {
            if (id != gym.Id) return BadRequest();

            if (!ModelState.IsValid)
                return View(gym);

            _context.Entry(gym).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();

                TempData["Success"] = "Spor salonu başarıyla güncellendi.";
                TempData["Msj"] = "Spor salonu başarıyla güncellendi.";

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Gyms.AnyAsync(g => g.Id == id))
                    return NotFound();

                TempData["Error"] = "Güncelleme sırasında bir hata oluştu.";
                TempData["HataMsj"] = "Güncelleme sırasında bir hata oluştu.";

                return View(gym);
            }
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
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var gym = await _context.Gyms.FindAsync(id);
            if (gym == null) return NotFound();

            return View(gym);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var gym = await _context.Gyms.FindAsync(id);
            if (gym == null)
            {
                TempData["Error"] = "Spor salonu bulunamadı.";
                TempData["HataMsj"] = "Spor salonu bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            _context.Gyms.Remove(gym);

            try
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = "Spor salonu başarıyla silindi.";
                TempData["Msj"] = "Spor salonu başarıyla silindi.";
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "Silme işlemi sırasında veritabanı hatası oluştu.";
                TempData["HataMsj"] = "Silme işlemi sırasında veritabanı hatası oluştu.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
