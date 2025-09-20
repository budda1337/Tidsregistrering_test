using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Tidsregistrering.Data;
using Tidsregistrering.Models;

namespace Tidsregistrering.Pages
{
    public class StatistikModel : PageModel
    {
        private readonly TidsregistreringContext _context;

        public StatistikModel(TidsregistreringContext context)
        {
            _context = context;
        }

        // Filter properties
        [BindProperty(SupportsGet = true)]
        public DateTime? FraDato { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? TilDato { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ValgtAfdeling { get; set; }

        // NYE FILTRE
        [BindProperty(SupportsGet = true)]
        public string? ValgtOuAfdeling { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ValgtSagsnummer { get; set; }

        // Current user info
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;

        // Total statistics for ALL users
        public int TotalRegistreringer { get; set; }
        public int TotalTimer { get; set; }
        public int TotalMinutter { get; set; }
        public int TotalMinutterAlt { get; set; }
        public int AntalBrugere { get; set; }
        public int AntalAfdelinger { get; set; }

        // Top users
        public List<BrugerStatModel> TopBrugere { get; set; } = new();

        // Department statistics
        public Dictionary<string, AfdelingStatModel> AfdelingStats { get; set; } = new();
        public string MestBrugteAfdeling { get; set; } = string.Empty;

        // Monthly activity
        public List<MånedStatModel> MånedligAktivitet { get; set; } = new();

        // Weekly activity (0 = Sunday, 1 = Monday, etc.)
        public Dictionary<int, UgeStatModel> UgentligAktivitet { get; set; } = new();

        // Date ranges
        public DateTime? FørsteRegistrering { get; set; }
        public DateTime? SenesteRegistrering { get; set; }

        // Available departments for filter
        public List<string> Afdelinger { get; set; } = new();

        // NYE LISTER til filtrering
        public List<string> OuAfdelinger { get; set; } = new();
        public List<string> Sagsnumre { get; set; } = new();

        // Calculated properties
        public decimal TotalTimerDecimal => Math.Round((decimal)TotalMinutterAlt / 60, 1);
        public decimal ArbejdsdageDecimal => Math.Round((decimal)TotalMinutterAlt / 480, 2);
        public decimal GennemsnitPerBruger => AntalBrugere > 0 ? Math.Round((decimal)TotalMinutterAlt / AntalBrugere / 60, 1) : 0;

        public async Task OnGetAsync()
        {
            LoadUserInfo();
            await LoadFilterOptionsAsync(); // NYT: Load filter options først
            await LoadStatisticsAsync();
        }

        private void LoadUserInfo()
        {
            Username = User.Identity?.Name ?? "Unknown";

            if (Username.Contains('\\'))
            {
                var parts = Username.Split('\\');
                FullName = parts[1];
            }
            else
            {
                FullName = Username;
            }
        }

        private async Task LoadFilterOptionsAsync()
        {
            // Hent aktive afdelinger fra MasterAfdelinger
            Afdelinger = await _context.MasterAfdelinger
                .Where(a => a.Aktiv)
                .OrderBy(a => a.Navn)
                .Select(a => a.Navn)
                .ToListAsync();

            // Fallback hvis MasterAfdelinger er tom
            if (!Afdelinger.Any())
            {
                Afdelinger = await _context.Registreringer
                    .Select(r => r.Afdeling)
                    .Distinct()
                    .OrderBy(a => a)
                    .ToListAsync();
            }

            // NYE FILTER LISTER
            OuAfdelinger = await _context.Registreringer
                .Where(r => !string.IsNullOrEmpty(r.OuAfdeling))
                .Select(r => r.OuAfdeling!)
                .Distinct()
                .OrderBy(ou => ou)
                .ToListAsync();

            Sagsnumre = await _context.Registreringer
                .Where(r => !string.IsNullOrEmpty(r.Sagsnummer))
                .Select(r => r.Sagsnummer!)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();
        }

        private async Task LoadStatisticsAsync()
        {
            // Build base query
            var query = _context.Registreringer.AsQueryable();

            // Apply filters
            if (FraDato.HasValue)
            {
                query = query.Where(r => r.Dato.Date >= FraDato.Value.Date);
            }

            if (TilDato.HasValue)
            {
                query = query.Where(r => r.Dato.Date <= TilDato.Value.Date);
            }

            if (!string.IsNullOrEmpty(ValgtAfdeling))
            {
                query = query.Where(r => r.Afdeling == ValgtAfdeling);
            }

            // NYE FILTRE
            if (!string.IsNullOrEmpty(ValgtOuAfdeling))
            {
                query = query.Where(r => r.OuAfdeling == ValgtOuAfdeling);
            }

            if (!string.IsNullOrEmpty(ValgtSagsnummer))
            {
                query = query.Where(r => r.Sagsnummer != null && r.Sagsnummer.Contains(ValgtSagsnummer));
            }

            var registreringer = await query
                .OrderByDescending(r => r.Dato)
                .ToListAsync();

            if (!registreringer.Any())
            {
                return;
            }

            // Overall statistics
            TotalRegistreringer = registreringer.Count;
            TotalMinutterAlt = registreringer.Sum(r => r.Minutter);
            TotalTimer = TotalMinutterAlt / 60;
            TotalMinutter = TotalMinutterAlt % 60;

            AntalBrugere = registreringer.Select(r => r.Brugernavn).Distinct().Count();

            // Date ranges
            FørsteRegistrering = registreringer.Min(r => r.Dato);
            SenesteRegistrering = registreringer.Max(r => r.Dato);

            // Top users
            await LoadTopBrugereAsync(registreringer);

            // Department statistics
            await LoadAfdelingStatsAsync(registreringer);

            // Monthly activity
            await LoadMånedligAktivitetAsync(registreringer);

            // Weekly activity
            await LoadUgentligAktivitetAsync(registreringer);
        }

        private async Task LoadTopBrugereAsync(List<Registrering> registreringer)
        {
            var brugerGroups = registreringer
                .GroupBy(r => new { r.Brugernavn, r.FuldeNavn })
                .Select(g => new BrugerStatModel
                {
                    Brugernavn = g.Key.Brugernavn,
                    FuldeNavn = g.Key.FuldeNavn ?? g.Key.Brugernavn.Split('\\').LastOrDefault() ?? g.Key.Brugernavn,
                    AntalRegistreringer = g.Count(),
                    TotalMinutter = g.Sum(r => r.Minutter),
                    Timer = g.Sum(r => r.Minutter) / 60,
                    Minutter = g.Sum(r => r.Minutter) % 60,
                    SenesteAktivitet = g.Max(r => r.Dato),
                    AntalAfdelinger = g.Select(r => r.Afdeling).Distinct().Count()
                })
                .OrderByDescending(b => b.TotalMinutter)
                .Take(10)
                .ToList();

            TopBrugere = brugerGroups;
        }

        private async Task LoadAfdelingStatsAsync(List<Registrering> registreringer)
        {
            var afdelingGroups = registreringer.GroupBy(r => r.Afdeling);

            foreach (var group in afdelingGroups)
            {
                var afdelingMinutter = group.Sum(r => r.Minutter);
                var procent = Math.Round((double)afdelingMinutter / TotalMinutterAlt * 100, 1);

                AfdelingStats[group.Key] = new AfdelingStatModel
                {
                    Antal = group.Count(),
                    TotalMinutter = afdelingMinutter,
                    Timer = afdelingMinutter / 60,
                    Minutter = afdelingMinutter % 60,
                    Procent = procent,
                    AntalBrugere = group.Select(r => r.Brugernavn).Distinct().Count()
                };
            }

            // Sort by most time
            AfdelingStats = AfdelingStats
                .OrderByDescending(x => x.Value.TotalMinutter)
                .ToDictionary(x => x.Key, x => x.Value);

            AntalAfdelinger = AfdelingStats.Count;
            MestBrugteAfdeling = AfdelingStats.FirstOrDefault().Key ?? string.Empty;
        }

        private async Task LoadMånedligAktivitetAsync(List<Registrering> registreringer)
        {
            var månedGroups = registreringer
                .GroupBy(r => new { r.Dato.Year, r.Dato.Month })
                .Select(g => new MånedStatModel
                {
                    År = g.Key.Year,
                    Måned = g.Key.Month,
                    MånedNavn = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMMM yyyy"),
                    AntalRegistreringer = g.Count(),
                    TotalMinutter = g.Sum(r => r.Minutter),
                    Timer = g.Sum(r => r.Minutter) / 60,
                    AntalBrugere = g.Select(r => r.Brugernavn).Distinct().Count()
                })
                .OrderBy(m => m.År)
                .ThenBy(m => m.Måned)
                .ToList();

            MånedligAktivitet = månedGroups;
        }

        private async Task LoadUgentligAktivitetAsync(List<Registrering> registreringer)
        {
            var ugeGroups = registreringer
                .GroupBy(r => (int)r.Dato.DayOfWeek)
                .ToDictionary(
                    g => g.Key,
                    g => new UgeStatModel
                    {
                        DagNavn = ((DayOfWeek)g.Key).ToString() switch
                        {
                            "Monday" => "Mandag",
                            "Tuesday" => "Tirsdag",
                            "Wednesday" => "Onsdag",
                            "Thursday" => "Torsdag",
                            "Friday" => "Fredag",
                            "Saturday" => "Lørdag",
                            "Sunday" => "Søndag",
                            _ => "Ukendt"
                        },
                        AntalRegistreringer = g.Count(),
                        TotalMinutter = g.Sum(r => r.Minutter),
                        Timer = g.Sum(r => r.Minutter) / 60
                    }
                );

            UgentligAktivitet = ugeGroups;
        }

        public async Task<IActionResult> OnPostExportAsync()
        {
            // TODO: Implement Excel export
            // This would require a library like EPPlus or ClosedXML
            TempData["Message"] = "Excel export funktionalitet kommer snart!";
            return RedirectToPage();
        }
    }

    public class BrugerStatModel
    {
        public string Brugernavn { get; set; } = string.Empty;
        public string FuldeNavn { get; set; } = string.Empty;
        public int AntalRegistreringer { get; set; }
        public int TotalMinutter { get; set; }
        public int Timer { get; set; }
        public int Minutter { get; set; }
        public DateTime SenesteAktivitet { get; set; }
        public int AntalAfdelinger { get; set; }
    }

    public class AfdelingStatModel
    {
        public int Antal { get; set; }
        public int TotalMinutter { get; set; }
        public int Timer { get; set; }
        public int Minutter { get; set; }
        public double Procent { get; set; }
        public int AntalBrugere { get; set; }
    }

    public class MånedStatModel
    {
        public int År { get; set; }
        public int Måned { get; set; }
        public string MånedNavn { get; set; } = string.Empty;
        public int AntalRegistreringer { get; set; }
        public int TotalMinutter { get; set; }
        public int Timer { get; set; }
        public int AntalBrugere { get; set; }
    }

    public class UgeStatModel
    {
        public string DagNavn { get; set; } = string.Empty;
        public int AntalRegistreringer { get; set; }
        public int TotalMinutter { get; set; }
        public int Timer { get; set; }
    }
}