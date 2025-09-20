using System.ComponentModel.DataAnnotations;

namespace Tidsregistrering.Models
{
    public class MasterAfdeling
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Navn { get; set; } = string.Empty;

        [Required]
        public bool Aktiv { get; set; } = true;

        public DateTime Oprettet { get; set; } = DateTime.Now;

        public DateTime? Opdateret { get; set; }

        [StringLength(100)]
        public string? OprettetAf { get; set; }

        [StringLength(100)]
        public string? OpdateretAf { get; set; }
    }
}