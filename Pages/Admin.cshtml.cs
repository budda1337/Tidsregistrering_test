using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Tidsregistrering.Data;
using Tidsregistrering.Models;

namespace Tidsregistrering.Pages
{
    public class AdminModel : PageModel
    {
        private readonly TidsregistreringContext _context;
        private readonly ILogger<AdminModel> _logger;

        public AdminModel(TidsregistreringContext context, ILogger<AdminModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Properties til visning af data
        public List<MasterAfdeling> MasterAfdelinger { get; set; } = new();
        public Dictionary<string, int> UsedAfdelinger { get; set; } = new();
        public List<Administrator> Administratorer { get; set; } = new();

        // Tjek admin adgang ved alle requests
        private async Task<bool> CheckAdminAccessAsync()
        {
            var isAdmin = await AdminConfig.IsAdminAsync(User, _context);

            if (!isAdmin)
            {
                _logger.LogWarning("Uautoriseret adgang til admin funktioner forsøgt af {User}",
                    User.Identity?.Name ?? "Unknown");
            }

            return isAdmin;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            // Tjek admin adgang
            if (!await CheckAdminAccessAsync())
            {
                TempData["Error"] = "Du har ikke adgang til administrationsområdet.";
                return RedirectToPage("/Index");
            }

            await LoadDataAsync();
            return Page();
        }

        private async Task LoadDataAsync()
        {
            // Hent alle master afdelinger
            MasterAfdelinger = await _context.MasterAfdelinger
                .OrderBy(m => m.Navn)
                .ToListAsync();

            // Hent alle unikke afdelingsnavne fra registreringer med antal
            UsedAfdelinger = await _context.Registreringer
                .GroupBy(r => r.Afdeling)
                .Select(g => new { Navn = g.Key, Antal = g.Count() })
                .ToDictionaryAsync(x => x.Navn, x => x.Antal);

            // Hent alle administratorer
            Administratorer = await _context.Administratorer
                .OrderBy(a => a.FuldeNavn ?? a.Brugernavn)
                .ToListAsync();
        }

        // Handler til at slå brugere op i Active Directory
        public async Task<IActionResult> OnPostLookupUserAsync()
        {
            // Tjek admin adgang
            if (!await CheckAdminAccessAsync())
            {
                return new JsonResult(new { success = false, message = "Ikke autoriseret." });
            }

            try
            {
                var requestBody = await new StreamReader(Request.Body).ReadToEndAsync();
                var data = JsonSerializer.Deserialize<UserLookupRequest>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (data?.Username == null || string.IsNullOrWhiteSpace(data.Username))
                {
                    return new JsonResult(new { success = false, message = "Brugernavn er påkrævet." });
                }

                var username = data.Username.Trim();
                var fullUsername = $"ibk\\{username}";

                // Tjek om brugeren allerede eksisterer som administrator
                var existingAdmin = await _context.Administratorer
                    .FirstOrDefaultAsync(a => a.Brugernavn.ToLower() == fullUsername.ToLower());

                if (existingAdmin != null)
                {
                    return new JsonResult(new { success = false, message = $"Brugeren '{username}' er allerede administrator." });
                }

                // Forsøg at slå brugeren op i Active Directory
                var userInfo = LookupUserInActiveDirectory(username);

                if (userInfo.Found)
                {
                    return new JsonResult(new
                    {
                        success = true,
                        displayName = userInfo.DisplayName,
                        message = $"Bruger '{username}' fundet i Active Directory."
                    });
                }
                else
                {
                    return new JsonResult(new { success = false, message = $"Brugeren '{username}' blev ikke fundet i Active Directory." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved opslag af bruger");
                return new JsonResult(new { success = false, message = "Der opstod en fejl ved opslag af brugeren." });
            }
        }

        // Hjælpemetode til Active Directory opslag
        private UserLookupResult LookupUserInActiveDirectory(string username)
        {
            try
            {
                using var context = new System.DirectoryServices.AccountManagement.PrincipalContext(
                    System.DirectoryServices.AccountManagement.ContextType.Domain, "ibk.lan");

                using var user = System.DirectoryServices.AccountManagement.UserPrincipal.FindByIdentity(
                    context, System.DirectoryServices.AccountManagement.IdentityType.SamAccountName, username);

                if (user != null)
                {
                    return new UserLookupResult
                    {
                        Found = true,
                        DisplayName = user.DisplayName ?? user.Name ?? username,
                        EmailAddress = user.EmailAddress
                    };
                }
                else
                {
                    return new UserLookupResult { Found = false };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Kunne ikke slå bruger op i Active Directory: {Username}", username);
                return new UserLookupResult { Found = false };
            }
        }

        // Handler til at tilføje administrator - OPDATERET VERSION
        public async Task<IActionResult> OnPostAddAdministratorAsync(string brugernavn, string fuldeNavn, bool aktiv = true, string bemærkninger = "")
        {
            // Tjek admin adgang
            if (!await CheckAdminAccessAsync())
            {
                TempData["Error"] = "Du har ikke adgang til administrationsområdet.";
                return RedirectToPage("/Index");
            }

            try
            {
                if (string.IsNullOrWhiteSpace(brugernavn))
                {
                    TempData["Error"] = "Brugernavn er påkrævet.";
                    await LoadDataAsync();
                    return Page();
                }

                // Tilføj ibk\ prefix hvis det ikke allerede er der
                var fullUsername = brugernavn.StartsWith("ibk\\", StringComparison.OrdinalIgnoreCase)
                    ? brugernavn
                    : $"ibk\\{brugernavn.Trim()}";

                // Check om brugernavnet allerede eksisterer
                var existingAdmin = await _context.Administratorer
                    .FirstOrDefaultAsync(a => a.Brugernavn.ToLower() == fullUsername.ToLower());

                if (existingAdmin != null)
                {
                    TempData["Error"] = $"En administrator med brugernavnet '{fullUsername}' eksisterer allerede.";
                    await LoadDataAsync();
                    return Page();
                }

                // Hvis fulde navn ikke er angivet, forsøg at slå det op
                if (string.IsNullOrWhiteSpace(fuldeNavn))
                {
                    var userInfo = LookupUserInActiveDirectory(brugernavn.Replace("ibk\\", ""));
                    fuldeNavn = userInfo.Found ? userInfo.DisplayName : null;
                }

                var newAdmin = new Administrator
                {
                    Brugernavn = fullUsername,
                    FuldeNavn = string.IsNullOrWhiteSpace(fuldeNavn) ? null : fuldeNavn.Trim(),
                    Aktiv = aktiv,
                    Oprettet = DateTime.Now,
                    OprettetAf = User.Identity?.Name ?? "Unknown",
                    Bemærkninger = string.IsNullOrWhiteSpace(bemærkninger) ? null : bemærkninger.Trim()
                };

                _context.Administratorer.Add(newAdmin);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Ny administrator oprettet: {Brugernavn} ({FuldeNavn}) af {User}",
                    fullUsername, fuldeNavn, User.Identity?.Name);

                TempData["Success"] = $"Administratoren '{fullUsername}' blev oprettet succesfuldt.";

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved oprettelse af administrator: {Brugernavn}", brugernavn);
                TempData["Error"] = "Der opstod en fejl ved oprettelse af administratoren.";
                await LoadDataAsync();
                return Page();
            }
        }

        // Handler til at redigere administrator
        public async Task<IActionResult> OnPostEditAdministratorAsync(int id, string brugernavn, string fuldeNavn, bool aktiv, string bemærkninger = "")
        {
            // Tjek admin adgang
            if (!await CheckAdminAccessAsync())
            {
                TempData["Error"] = "Du har ikke adgang til administrationsområdet.";
                return RedirectToPage("/Index");
            }

            try
            {
                if (string.IsNullOrWhiteSpace(brugernavn))
                {
                    TempData["Error"] = "Brugernavn er påkrævet.";
                    await LoadDataAsync();
                    return Page();
                }

                var administrator = await _context.Administratorer.FindAsync(id);
                if (administrator == null)
                {
                    TempData["Error"] = "Administratoren blev ikke fundet.";
                    await LoadDataAsync();
                    return Page();
                }

                // Check om det nye brugernavn allerede eksisterer (undtagen den nuværende)
                var existingAdmin = await _context.Administratorer
                    .FirstOrDefaultAsync(a => a.Brugernavn.ToLower() == brugernavn.ToLower() && a.Id != id);

                if (existingAdmin != null)
                {
                    TempData["Error"] = $"En anden administrator med brugernavnet '{brugernavn}' eksisterer allerede.";
                    await LoadDataAsync();
                    return Page();
                }

                var oldBrugernavn = administrator.Brugernavn;
                administrator.Brugernavn = brugernavn.Trim();
                administrator.FuldeNavn = string.IsNullOrWhiteSpace(fuldeNavn) ? null : fuldeNavn.Trim();
                administrator.Aktiv = aktiv;
                administrator.Opdateret = DateTime.Now;
                administrator.OpdateretAf = User.Identity?.Name ?? "Unknown";
                administrator.Bemærkninger = string.IsNullOrWhiteSpace(bemærkninger) ? null : bemærkninger.Trim();

                await _context.SaveChangesAsync();

                _logger.LogInformation("Administrator opdateret: {OldBrugernavn} -> {NewBrugernavn} af {User}",
                    oldBrugernavn, brugernavn, User.Identity?.Name);

                TempData["Success"] = $"Administratoren blev opdateret succesfuldt.";

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved opdatering af administrator ID: {Id}", id);
                TempData["Error"] = "Der opstod en fejl ved opdatering af administratoren.";
                await LoadDataAsync();
                return Page();
            }
        }

        // Handler til at slette administrator
        public async Task<IActionResult> OnPostDeleteAdministratorAsync()
        {
            // Tjek admin adgang
            if (!await CheckAdminAccessAsync())
            {
                return new JsonResult(new { success = false, message = "Du har ikke adgang til administrationsområdet." });
            }

            try
            {
                var requestBody = await new StreamReader(Request.Body).ReadToEndAsync();
                var data = JsonSerializer.Deserialize<DeleteRequest>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (data?.Id == null)
                {
                    return new JsonResult(new { success = false, message = "Ugyldig forespørgsel." });
                }

                var administrator = await _context.Administratorer.FindAsync(data.Id);
                if (administrator == null)
                {
                    return new JsonResult(new { success = false, message = "Administratoren blev ikke fundet." });
                }

                // Tjek om dette er den sidste aktive administrator
                var activeAdminCount = await _context.Administratorer.CountAsync(a => a.Aktiv);
                if (activeAdminCount <= 1 && administrator.Aktiv)
                {
                    return new JsonResult(new
                    {
                        success = false,
                        message = "Kan ikke slette den sidste aktive administrator. Der skal altid være mindst én aktiv administrator."
                    });
                }

                _context.Administratorer.Remove(administrator);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Administrator slettet: {Brugernavn} af {User}", administrator.Brugernavn, User.Identity?.Name);

                return new JsonResult(new { success = true, message = $"Administratoren '{administrator.Brugernavn}' blev slettet." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved sletning af administrator");
                return new JsonResult(new { success = false, message = "Der opstod en fejl ved sletning af administratoren." });
            }
        }

        // Handler til at tilføje ny master afdeling
        public async Task<IActionResult> OnPostAddAfdelingAsync(string navn, bool aktiv = true)
        {
            // Tjek admin adgang
            if (!await CheckAdminAccessAsync())
            {
                TempData["Error"] = "Du har ikke adgang til administrationsområdet.";
                return RedirectToPage("/Index");
            }

            try
            {
                if (string.IsNullOrWhiteSpace(navn))
                {
                    TempData["Error"] = "Afdelingsnavn er påkrævet.";
                    await LoadDataAsync();
                    return Page();
                }

                // Check om navnet allerede eksisterer
                var existingAfdeling = await _context.MasterAfdelinger
                    .FirstOrDefaultAsync(m => m.Navn.ToLower() == navn.ToLower());

                if (existingAfdeling != null)
                {
                    TempData["Error"] = $"En afdeling med navnet '{navn}' eksisterer allerede.";
                    await LoadDataAsync();
                    return Page();
                }

                var newAfdeling = new MasterAfdeling
                {
                    Navn = navn.Trim(),
                    Aktiv = aktiv,
                    Oprettet = DateTime.Now,
                    OprettetAf = User.Identity?.Name ?? "Unknown"
                };

                _context.MasterAfdelinger.Add(newAfdeling);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Ny master afdeling oprettet: {Navn} af {User}", navn, User.Identity?.Name);
                TempData["Success"] = $"Afdelingen '{navn}' blev oprettet succesfuldt.";

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved oprettelse af master afdeling: {Navn}", navn);
                TempData["Error"] = "Der opstod en fejl ved oprettelse af afdelingen.";
                await LoadDataAsync();
                return Page();
            }
        }

        // Handler til at redigere master afdeling
        public async Task<IActionResult> OnPostEditAfdelingAsync(int id, string navn, bool aktiv)
        {
            // Tjek admin adgang
            if (!await CheckAdminAccessAsync())
            {
                TempData["Error"] = "Du har ikke adgang til administrationsområdet.";
                return RedirectToPage("/Index");
            }

            try
            {
                if (string.IsNullOrWhiteSpace(navn))
                {
                    TempData["Error"] = "Afdelingsnavn er påkrævet.";
                    await LoadDataAsync();
                    return Page();
                }

                var afdeling = await _context.MasterAfdelinger.FindAsync(id);
                if (afdeling == null)
                {
                    TempData["Error"] = "Afdelingen blev ikke fundet.";
                    await LoadDataAsync();
                    return Page();
                }

                var oldNavn = afdeling.Navn;
                var newNavn = navn.Trim();

                // Hvis navnet ændres
                if (!oldNavn.Equals(newNavn, StringComparison.OrdinalIgnoreCase))
                {
                    // Check om det nye navn allerede eksisterer
                    var existingAfdeling = await _context.MasterAfdelinger
                        .FirstOrDefaultAsync(m => m.Navn.ToLower() == newNavn.ToLower());

                    if (existingAfdeling != null)
                    {
                        TempData["Error"] = $"En afdeling med navnet '{newNavn}' eksisterer allerede.";
                        await LoadDataAsync();
                        return Page();
                    }

                    // Gør den gamle afdeling inaktiv (bevarer historik)
                    afdeling.Aktiv = false;
                    afdeling.Opdateret = DateTime.Now;
                    afdeling.OpdateretAf = User.Identity?.Name ?? "Unknown";

                    // Opret ny master afdeling med det nye navn
                    var newAfdeling = new MasterAfdeling
                    {
                        Navn = newNavn,
                        Aktiv = aktiv,
                        Oprettet = DateTime.Now,
                        OprettetAf = User.Identity?.Name ?? "Unknown"
                    };

                    _context.MasterAfdelinger.Add(newAfdeling);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Master afdeling navneændring: '{OldNavn}' gjort inaktiv, ny afdeling '{NewNavn}' oprettet af {User}",
                        oldNavn, newNavn, User.Identity?.Name);

                    TempData["Success"] = $"Afdelingen '{oldNavn}' blev gjort inaktiv og ny afdeling '{newNavn}' blev oprettet. Eksisterende registreringer bevarer deres oprindelige afdelingsnavn.";
                }
                else
                {
                    // Kun status ændring - ingen navneændring
                    afdeling.Aktiv = aktiv;
                    afdeling.Opdateret = DateTime.Now;
                    afdeling.OpdateretAf = User.Identity?.Name ?? "Unknown";

                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Master afdeling status opdateret: '{Navn}' sat til {Status} af {User}",
                        afdeling.Navn, aktiv ? "Aktiv" : "Inaktiv", User.Identity?.Name);

                    TempData["Success"] = $"Afdelingen '{afdeling.Navn}' blev sat til {(aktiv ? "aktiv" : "inaktiv")}.";
                }

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved opdatering af master afdeling ID: {Id}", id);
                TempData["Error"] = "Der opstod en fejl ved opdatering af afdelingen.";
                await LoadDataAsync();
                return Page();
            }
        }

        // Handler til at slette master afdeling
        public async Task<IActionResult> OnPostDeleteAfdelingAsync()
        {
            // Tjek admin adgang
            if (!await CheckAdminAccessAsync())
            {
                return new JsonResult(new { success = false, message = "Du har ikke adgang til administrationsområdet." });
            }

            try
            {
                var requestBody = await new StreamReader(Request.Body).ReadToEndAsync();
                var data = JsonSerializer.Deserialize<DeleteRequest>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (data?.Id == null)
                {
                    return new JsonResult(new { success = false, message = "Ugyldig forespørgsel." });
                }

                var afdeling = await _context.MasterAfdelinger.FindAsync(data.Id);
                if (afdeling == null)
                {
                    return new JsonResult(new { success = false, message = "Afdelingen blev ikke fundet." });
                }

                // Check om der er aktive registreringer på denne afdeling
                var activeRegistrations = await _context.Registreringer
                    .CountAsync(r => r.Afdeling == afdeling.Navn);

                if (activeRegistrations > 0)
                {
                    return new JsonResult(new
                    {
                        success = false,
                        message = $"Kan ikke slette afdelingen '{afdeling.Navn}' da der er {activeRegistrations} aktive registreringer. Brug masseændring-funktionen først."
                    });
                }

                _context.MasterAfdelinger.Remove(afdeling);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Master afdeling slettet: {Navn} af {User}", afdeling.Navn, User.Identity?.Name);

                return new JsonResult(new { success = true, message = $"Afdelingen '{afdeling.Navn}' blev slettet." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved sletning af master afdeling");
                return new JsonResult(new { success = false, message = "Der opstod en fejl ved sletning af afdelingen." });
            }
        }

        // Handler til at forhåndsvise masseændring
        public async Task<IActionResult> OnPostPreviewMassChangeAsync()
        {
            // Tjek admin adgang
            if (!await CheckAdminAccessAsync())
            {
                return new JsonResult(new { success = false, message = "Du har ikke adgang til administrationsområdet." });
            }

            try
            {
                var requestBody = await new StreamReader(Request.Body).ReadToEndAsync();
                var data = JsonSerializer.Deserialize<MassChangeRequest>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (data == null || string.IsNullOrWhiteSpace(data.OldName) || string.IsNullOrWhiteSpace(data.NewName))
                {
                    return new JsonResult(new { success = false, message = "Ugyldige parametre." });
                }

                if (data.OldName.Equals(data.NewName, StringComparison.OrdinalIgnoreCase))
                {
                    return new JsonResult(new { success = false, message = "Det gamle og nye navn kan ikke være ens." });
                }

                // Hent alle registreringer der vil blive påvirket
                var affectedRegistrations = await _context.Registreringer
                    .Where(r => r.Afdeling == data.OldName)
                    .ToListAsync();

                if (!affectedRegistrations.Any())
                {
                    return new JsonResult(new { success = false, message = $"Ingen registreringer fundet med afdelingen '{data.OldName}'." });
                }

                // Gruppér efter bruger
                var affectedUsers = affectedRegistrations
                    .GroupBy(r => new { r.Brugernavn, r.FuldeNavn })
                    .Select(g => new
                    {
                        name = g.Key.FuldeNavn ?? g.Key.Brugernavn,
                        count = g.Count()
                    })
                    .OrderBy(u => u.name)
                    .ToList();

                return new JsonResult(new
                {
                    success = true,
                    oldName = data.OldName,
                    newName = data.NewName,
                    affectedCount = affectedRegistrations.Count,
                    affectedUsers = affectedUsers
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved forhåndsvisning af masseændring");
                return new JsonResult(new { success = false, message = "Der opstod en fejl ved forhåndsvisning." });
            }
        }

        // Handler til at udføre masseændring
        public async Task<IActionResult> OnPostExecuteMassChangeAsync()
        {
            // Tjek admin adgang
            if (!await CheckAdminAccessAsync())
            {
                return new JsonResult(new { success = false, message = "Du har ikke adgang til administrationsområdet." });
            }

            try
            {
                var requestBody = await new StreamReader(Request.Body).ReadToEndAsync();
                var data = JsonSerializer.Deserialize<MassChangeRequest>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (data == null || string.IsNullOrWhiteSpace(data.OldName) || string.IsNullOrWhiteSpace(data.NewName))
                {
                    return new JsonResult(new { success = false, message = "Ugyldige parametre." });
                }

                if (data.OldName.Equals(data.NewName, StringComparison.OrdinalIgnoreCase))
                {
                    return new JsonResult(new { success = false, message = "Det gamle og nye navn kan ikke være ens." });
                }

                // Hent alle registreringer der skal ændres
                var registrationsToUpdate = await _context.Registreringer
                    .Where(r => r.Afdeling == data.OldName)
                    .ToListAsync();

                if (!registrationsToUpdate.Any())
                {
                    return new JsonResult(new { success = false, message = $"Ingen registreringer fundet med afdelingen '{data.OldName}'." });
                }

                // 1. Håndter master afdelinger

                // Find den gamle master afdeling
                var oldMasterAfdeling = await _context.MasterAfdelinger
                    .FirstOrDefaultAsync(m => m.Navn == data.OldName);

                // Gør den gamle master afdeling inaktiv (hvis den findes)
                if (oldMasterAfdeling != null)
                {
                    oldMasterAfdeling.Aktiv = false;
                    oldMasterAfdeling.Opdateret = DateTime.Now;
                    oldMasterAfdeling.OpdateretAf = User.Identity?.Name ?? "Unknown";
                }

                // Check om der allerede eksisterer en master afdeling med det nye navn
                var existingNewMasterAfdeling = await _context.MasterAfdelinger
                    .FirstOrDefaultAsync(m => m.Navn == data.NewName);

                if (existingNewMasterAfdeling != null)
                {
                    // Master afdeling findes allerede - gør den aktiv
                    existingNewMasterAfdeling.Aktiv = true;
                    existingNewMasterAfdeling.Opdateret = DateTime.Now;
                    existingNewMasterAfdeling.OpdateretAf = User.Identity?.Name ?? "Unknown";
                }
                else
                {
                    // Opret ny master afdeling
                    var newMasterAfdeling = new MasterAfdeling
                    {
                        Navn = data.NewName,
                        Aktiv = true,
                        Oprettet = DateTime.Now,
                        OprettetAf = User.Identity?.Name ?? "Unknown"
                    };
                    _context.MasterAfdelinger.Add(newMasterAfdeling);
                }

                // 2. Opdater alle registreringer
                foreach (var registration in registrationsToUpdate)
                {
                    registration.Afdeling = data.NewName;
                }

                // Gem alle ændringer
                await _context.SaveChangesAsync();

                var masterAfdelingMessage = oldMasterAfdeling != null
                    ? $" Master afdeling '{data.OldName}' er gjort inaktiv og '{data.NewName}' er oprettet/aktiveret."
                    : $" Master afdeling '{data.NewName}' er oprettet.";

                _logger.LogWarning("Masseændring udført: '{OldName}' -> '{NewName}' på {Count} registreringer af {User}.{MasterMessage}",
                    data.OldName, data.NewName, registrationsToUpdate.Count, User.Identity?.Name, masterAfdelingMessage);

                return new JsonResult(new
                {
                    success = true,
                    changedCount = registrationsToUpdate.Count,
                    message = $"Masseændring udført succesfuldt. {registrationsToUpdate.Count} registreringer blev opdateret.{masterAfdelingMessage}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved udførelse af masseændring");
                return new JsonResult(new { success = false, message = "Der opstod en fejl ved udførelse af masseændringen." });
            }
        }

        // Endpoint til at tjekke admin status for navigation
        public async Task<IActionResult> OnGetCheckAdminStatusAsync()
        {
            var isAdmin = await CheckAdminAccessAsync();
            return new JsonResult(new { isAdmin = isAdmin });
        }
    }

    // Helper classes til JSON deserialization
    public class MassChangeRequest
    {
        public string OldName { get; set; } = string.Empty;
        public string NewName { get; set; } = string.Empty;
    }

    public class UserLookupRequest
    {
        public string Username { get; set; } = string.Empty;
    }

    public class UserLookupResult
    {
        public bool Found { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string? EmailAddress { get; set; }
    }

    public class DeleteRequest
    {
        public int Id { get; set; }
    }
}