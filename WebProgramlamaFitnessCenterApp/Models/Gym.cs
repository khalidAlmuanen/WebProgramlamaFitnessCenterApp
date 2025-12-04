using System.ComponentModel.DataAnnotations;

namespace FitnessCenterApp.Models
{
    public class Gym
    {
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; }

        [MaxLength(250)]
        public string? Location { get; set; }

        public string? Description { get; set; }

        [Required]
        public TimeSpan OpeningTime { get; set; }

        [Required]
        public TimeSpan ClosingTime { get; set; }

    }
}
