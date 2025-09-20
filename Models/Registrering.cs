using System.ComponentModel.DataAnnotations;

namespace Tidsregistrering.Models
{
    public class Registrering
    {
        public int Id { get; set; }

        [Required]
        public DateTime Dato { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Minutter skal være større end 0")]
        public int Minutter { get; set; }

        [Required]
        [StringLength(100)]
        public string Afdeling { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Bemærkninger { get; set; }

        [Required]
        [StringLength(50)]
        public string Brugernavn { get; set; } = string.Empty;

        [StringLength(100)]
        public string? FuldeNavn { get; set; }

        [StringLength(100)]
        public string? OuAfdeling { get; set; }

        public DateTime? DatoUdført { get; set; }

        public DateTime Oprettet { get; set; } = DateTime.Now;

        // NYT FELT - Sagsnummer
        [StringLength(60)]
        public string? Sagsnummer { get; set; }
    }
}