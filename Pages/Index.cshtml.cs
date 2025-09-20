using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.DirectoryServices.AccountManagement;
using Tidsregistrering.Data;
using Tidsregistrering.Models;

namespace Tidsregistrering.Pages
{
    public class IndexModel : PageModel
    {
        private readonly TidsregistreringContext _context;

        public IndexModel(TidsregistreringContext context)
        {
            _context = context;
        }

        // Properties til visning
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Department { get; set; } = "IT-afdelingen";
        public int TotalHours { get; set; }
        public int TotalMinutes { get; set; }
        public List<string> Afdelinger { get; set; } = new();
        public List<Registrering> RecentRegistreringer { get; set; } = new();

        // Form properties
        [BindProperty]
        public NewRegistreringModel NewRegistrering { get; set; } = new();

        public async Task OnGetAsync()
        {
            LoadUserInfo();
            await LoadAfdelingerAsync();
            await LoadUserStatsAsync();
            await LoadRecentRegistreringerAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Load user info og afdelinger F�RST
            LoadUserInfo();
            await LoadAfdelingerAsync();

            if (!ModelState.IsValid)
            {
                await LoadUserStatsAsync();
                await LoadRecentRegistreringerAsync();
                return Page();
            }

            // Til database gemmes det fulde username
            var fullUsername = User.Identity?.Name ?? "Unknown";

            // Opret ny registrering
            var registrering = new Registrering
            {
                Dato = DateTime.Now,
                Minutter = NewRegistrering.Minutter,
                Afdeling = NewRegistrering.Afdeling!,
                Bem�rkninger = NewRegistrering.Bem�rkninger,
                Sagsnummer = NewRegistrering.Sagsnummer, // NYT - Tilf�j sagsnummer
                Brugernavn = fullUsername,  // Fulde username til database
                FuldeNavn = FullName,       // P�nt navn til visning
                OuAfdeling = Department,
                DatoUdf�rt = NewRegistrering.DatoUdf�rt ?? DateTime.Today,
                Oprettet = DateTime.Now
            };

            _context.Registreringer.Add(registrering);
            await _context.SaveChangesAsync();

            ViewData["Message"] = $"Registrering gemt! {NewRegistrering.Minutter} minutter p� {NewRegistrering.Afdeling}.";

            // Reset form
            NewRegistrering = new NewRegistreringModel();

            await LoadAfdelingerAsync();
            await LoadUserStatsAsync();
            await LoadRecentRegistreringerAsync();

            return Page();
        }

        private async Task LoadAfdelingerAsync()
        {
            // Hent alle aktive afdelinger fra MasterAfdelinger tabellen
            Afdelinger = await _context.MasterAfdelinger
                .Where(a => a.Aktiv)
                .Select(a => a.Navn)
                .OrderBy(a => a)
                .ToListAsync();

            // Fallback til hardcoded liste hvis ingen afdelinger i master tabel
            if (!Afdelinger.Any())
            {
                Afdelinger = AfdelingerConfig.Afdelinger;
            }
        }

        private void LoadUserInfo()
        {
            var rawUsername = User.Identity?.Name ?? "Unknown";

            // Fjern domain prefix (IBK\) fra username til visning
            Username = rawUsername.Contains('\\') ? rawUsername.Split('\\')[1] : rawUsername;

            // Hent AD info
            try
            {
                var usernamePart = rawUsername.Contains('\\') ? rawUsername.Split('\\')[1] : rawUsername;

                using var context = new PrincipalContext(ContextType.Domain, "ibk.lan");
                var userPrincipal = UserPrincipal.FindByIdentity(context, usernamePart);

                if (userPrincipal != null)
                {
                    FullName = userPrincipal.DisplayName ?? usernamePart;
                    Department = GetDepartmentFromAD(userPrincipal) ?? "Unknown";

                    // Debug info
                    Console.WriteLine($"AD Success - DisplayName: {userPrincipal.DisplayName}");
                    Console.WriteLine($"AD Success - Department: {Department}");
                }
                else
                {
                    FullName = usernamePart;
                    Department = "Unknown";
                    Console.WriteLine("AD: User not found");
                }
            }
            catch (Exception ex)
            {
                FullName = rawUsername.Contains('\\') ? rawUsername.Split('\\')[1] : rawUsername;
                Department = "Unknown";
                Console.WriteLine($"AD Error: {ex.Message}");
            }
        }

        private string? GetDepartmentFromAD(UserPrincipal userPrincipal)
        {
            try
            {
                // Pr�v forskellige properties for afdeling
                var directoryEntry = userPrincipal.GetUnderlyingObject() as System.DirectoryServices.DirectoryEntry;

                // Pr�v department property
                if (directoryEntry?.Properties["department"]?.Value != null)
                {
                    return directoryEntry.Properties["department"].Value.ToString();
                }

                // Pr�v OU fra Distinguished Name
                if (!string.IsNullOrEmpty(userPrincipal.DistinguishedName))
                {
                    var dn = userPrincipal.DistinguishedName;
                    var ouMatch = System.Text.RegularExpressions.Regex.Match(dn, @"OU=([^,]+)");
                    if (ouMatch.Success)
                    {
                        return ouMatch.Groups[1].Value;
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private async Task LoadUserStatsAsync()
        {
            // Til database query bruger vi det fulde username (med domain)
            var fullUsername = User.Identity?.Name ?? "Unknown";

            var userRegistreringer = await _context.Registreringer
                .Where(r => r.Brugernavn == fullUsername)
                .OrderByDescending(r => r.Dato)
                .ToListAsync();

            // Beregn total tid
            var totalMinutter = userRegistreringer.Sum(r => r.Minutter);
            TotalHours = totalMinutter / 60;
            TotalMinutes = totalMinutter % 60;

            // Hent seneste 5 registreringer
            RecentRegistreringer = userRegistreringer.Take(5).ToList();
        }

        private async Task LoadRecentRegistreringerAsync()
        {
            // Dette metode kaldes nu fra LoadUserStatsAsync for at undg� dublering
        }
    }

    public class NewRegistreringModel
    {
        [Required(ErrorMessage = "Minutter er p�kr�vet")]
        [Range(1, int.MaxValue, ErrorMessage = "Minutter skal v�re st�rre end 0")]
        public int Minutter { get; set; }

        [Required(ErrorMessage = "Afdeling skal v�lges")]
        public string? Afdeling { get; set; }

        [StringLength(1000, ErrorMessage = "Bem�rkninger m� maksimalt v�re 1000 tegn")]
        public string? Bem�rkninger { get; set; }

        public DateTime? DatoUdf�rt { get; set; }

        // NYT FELT - Sagsnummer
        [StringLength(60, ErrorMessage = "Sagsnummer m� maksimalt v�re 60 tegn")]
        public string? Sagsnummer { get; set; }
    }
}