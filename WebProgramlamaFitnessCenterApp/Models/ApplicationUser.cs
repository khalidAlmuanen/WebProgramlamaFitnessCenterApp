using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace WebProgramlamaFitnessCenterApp.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        public string FullName { get; set; } = string.Empty;

        public int? Age { get; set; }

        public double? Height { get; set; }   
        public double? Weight { get; set; }   

    }
}
