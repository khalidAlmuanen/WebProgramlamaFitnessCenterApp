using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebProgramlamaFitnessCenterApp.Models;

namespace WebProgramlamaFitnessCenterApp.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            await context.Database.MigrateAsync();

            string[] roles = { "Admin", "Uye" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    var roleResult = await roleManager.CreateAsync(new IdentityRole(role));
                    if (!roleResult.Succeeded)
                    {
                        var msg = string.Join(" | ", roleResult.Errors.Select(e => $"{e.Code}: {e.Description}"));
                        throw new Exception($"Role oluşturulamadı: {role}. {msg}");
                    }
                }
            }

            var adminEmail = "G211210575@sakarya.edu.tr";
            var adminPassword = "sau";

            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "Yönetici Admin",
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(adminUser, adminPassword);
                if (!createResult.Succeeded)
                {
                    var msg = string.Join(" | ", createResult.Errors.Select(e => $"{e.Code}: {e.Description}"));
                    throw new Exception("Admin kullanıcı oluşturulamadı. " + msg);
                }

                adminUser = await userManager.FindByEmailAsync(adminEmail);
                if (adminUser == null)
                    throw new Exception("Admin oluşturuldu ama DB'den okunamadı.");
            }
            else
            {
                var resetToken = await userManager.GeneratePasswordResetTokenAsync(adminUser);
                var resetResult = await userManager.ResetPasswordAsync(adminUser, resetToken, adminPassword);

                if (!resetResult.Succeeded)
                {
                    var msg = string.Join(" | ", resetResult.Errors.Select(e => $"{e.Code}: {e.Description}"));
                    throw new Exception("Admin şifresi güncellenemedi. " + msg);
                }
            }

            if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
            {
                var addRoleResult = await userManager.AddToRoleAsync(adminUser, "Admin");
                if (!addRoleResult.Succeeded)
                {
                    var msg = string.Join(" | ", addRoleResult.Errors.Select(e => $"{e.Code}: {e.Description}"));
                    throw new Exception("Admin rolü atanamadı. " + msg);
                }
            }
        }
    }
}
