using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.DirectoryServices.AccountManagement;
using Tidsregistrering.Data;
using Tidsregistrering.Models;

namespace Tidsregistrering.Pages
{
    public class RegistreringerModel : PageModel
    {
        private readonly TidsregistreringContext _context;

        public RegistreringerModel(TidsregistreringContext context)
        {
            _context = context;
        }

        public List<Registrering> Registreringer { get; set; } = new();
        public string FullName { get; set; } = string.Empty;
        // NYT: Tilføj afdelinger property
        public List<string> Afdelinger { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadUserRegistreringerAsync();
            await LoadAfdelingerAsync(); // NYT: Load afdelinger
            LoadUserInfo();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var registrering = await _context.Registreringer.FindAsync(id);

            if (registrering != null && registrering.Brugernavn == User.Identity?.Name)
            {
                _context.Registreringer.Remove(registrering);
                await _context.SaveChangesAsync();
                TempData["Message"] = "Registrering slettet succesfuldt.";
            }

            await LoadUserRegistreringerAsync();
            await LoadAfdelingerAsync(); // NYT: Reload afdelinger
            LoadUserInfo();
            return Page();
        }

        public async Task<IActionResult> OnPostEditAsync(int editId, int editMinutter, string editAfdeling, string? editBemærkninger, DateTime? editDatoUdført, string? editSagsnummer)
        {
            var registrering = await _context.Registreringer.FindAsync(editId);

            if (registrering != null && registrering.Brugernavn == User.Identity?.Name)
            {
                registrering.Minutter = editMinutter;
                registrering.Afdeling = editAfdeling;
                registrering.Bemærkninger = editBemærkninger;
                registrering.DatoUdført = editDatoUdført;
                registrering.Sagsnummer = editSagsnummer; // NYT - Tilføj denne linje

                await _context.SaveChangesAsync();
                TempData["Message"] = "Registrering opdateret succesfuldt.";
            }

            await LoadUserRegistreringerAsync();
            await LoadAfdelingerAsync();
            LoadUserInfo();
            return Page();
        }

        private async Task LoadUserRegistreringerAsync()
        {
            var fullUsername = User.Identity?.Name ?? "Unknown";

            Registreringer = await _context.Registreringer
                .Where(r => r.Brugernavn == fullUsername)
                .OrderByDescending(r => r.Dato)
                .ToListAsync();
        }

        // NYT: Load afdelinger fra database
        private async Task LoadAfdelingerAsync()
        {
            try
            {
                Afdelinger = await _context.MasterAfdelinger
                    .Where(a => a.Aktiv)
                    .OrderBy(a => a.Navn)
                    .Select(a => a.Navn)
                    .ToListAsync();

                // Fallback til hardcoded liste hvis ingen afdelinger i master tabel
                if (!Afdelinger.Any())
                {
                    Afdelinger = AfdelingerConfig.Afdelinger;
                }
            }
            catch (Exception)
            {
                // Fallback hvis database fejler
                Afdelinger = AfdelingerConfig.Afdelinger;
            }
        }

        private void LoadUserInfo()
        {
            var rawUsername = User.Identity?.Name ?? "Unknown";
            var usernamePart = rawUsername.Contains('\\') ? rawUsername.Split('\\')[1] : rawUsername;

            try
            {
                using var context = new PrincipalContext(ContextType.Domain, "ibk.lan");
                var userPrincipal = UserPrincipal.FindByIdentity(context, usernamePart);

                FullName = userPrincipal?.DisplayName ?? usernamePart;
            }
            catch
            {
                FullName = usernamePart;
            }
        }
    }
}