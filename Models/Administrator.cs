using System.ComponentModel.DataAnnotations;

namespace Tidsregistrering.Models
{
    public class Administrator
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Brugernavn { get; set; } = string.Empty;

        [StringLength(100)]
        public string? FuldeNavn { get; set; }

        [Required]
        public bool Aktiv { get; set; } = true;

        public DateTime Oprettet { get; set; } = DateTime.Now;

        public DateTime? Opdateret { get; set; }

        [StringLength(100)]
        public string? OprettetAf { get; set; }

        [StringLength(100)]
        public string? OpdateretAf { get; set; }

        [StringLength(200)]
        public string? Bemærkninger { get; set; }
    }
}