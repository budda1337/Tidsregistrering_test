using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Tidsregistrering.Data;
using Tidsregistrering.Models;

namespace Tidsregistrering.Pages
{
    public class OversigtModel : PageModel
    {
        private readonly TidsregistreringContext _context;

        public OversigtModel(TidsregistreringContext context)
        {
            _context = context;
        }

        // Existing properties
        public List<Registrering> AlleRegistreringer { get; set; } = new();
        public List<string> Afdelinger { get; set; } = new();
        public List<string> Brugere { get; set; } = new();

        // Filter properties
        [BindProperty(SupportsGet = true)]
        public DateTime? FraDato { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? TilDato { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ValgtAfdeling { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ValgtBruger { get; set; }

        // NYE FILTRE
        [BindProperty(SupportsGet = true)]
        public string? ValgtOuAfdeling { get; set; } // OU afdeling filter

        [BindProperty(SupportsGet = true)]
        public string? ValgtSagsnummer { get; set; } // Sagsnummer filter

        // Statistics properties
        public int TotalAntalRegistreringer { get; set; }
        public string TotalTimerDecimal { get; set; } = "0,0";

        // Sorting
        [BindProperty(SupportsGet = true)]
        public string SortBy { get; set; } = "Dato";

        [BindProperty(SupportsGet = true)]
        public bool SortDescending { get; set; } = true;

        // NYE LISTER til filtrering
        public List<string> OuAfdelinger { get; set; } = new(); // Liste over OU afdelinger
        public List<string> Sagsnumre { get; set; } = new(); // Liste over sagsnumre

        public async Task OnGetAsync()
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            // Start med alle registreringer
            var query = _context.Registreringer.AsQueryable();

            // Anvend filtre
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

            if (!string.IsNullOrEmpty(ValgtBruger))
            {
                query = query.Where(r => r.FuldeNavn == ValgtBruger);
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

            // Sortér
            query = SortBy switch
            {
                "Bruger" => SortDescending ? query.OrderByDescending(r => r.FuldeNavn) : query.OrderBy(r => r.FuldeNavn),
                "Afdeling" => SortDescending ? query.OrderByDescending(r => r.Afdeling) : query.OrderBy(r => r.Afdeling),
                "Tid" => SortDescending ? query.OrderByDescending(r => r.Minutter) : query.OrderBy(r => r.Minutter),
                _ => SortDescending ? query.OrderByDescending(r => r.Dato) : query.OrderBy(r => r.Dato)
            };

            // Hent filtrerede data
            AlleRegistreringer = await query.ToListAsync();

            // Beregn statistikker
            TotalAntalRegistreringer = AlleRegistreringer.Count;
            var totalMinutter = AlleRegistreringer.Sum(r => r.Minutter);
            TotalTimerDecimal = $"{totalMinutter / 60:N1}";

            // Load filter options
            await LoadFilterOptionsAsync();
        }

        private async Task LoadFilterOptionsAsync()
        {
            // Hent aktive afdelinger fra MasterAfdelinger
            Afdelinger = await _context.MasterAfdelinger
                .Where(a => a.Aktiv)
                .OrderBy(a => a.Navn)
                .Select(a => a.Navn)
                .ToListAsync();

            // Hent unikke brugere (fulde navne)
            Brugere = await _context.Registreringer
                .Where(r => r.FuldeNavn != null)
                .Select(r => r.FuldeNavn!)
                .Distinct()
                .OrderBy(b => b)
                .ToListAsync();

            // NYE FILTER LISTER
            // Hent unikke OU afdelinger
            OuAfdelinger = await _context.Registreringer
                .Where(r => !string.IsNullOrEmpty(r.OuAfdeling))
                .Select(r => r.OuAfdeling!)
                .Distinct()
                .OrderBy(ou => ou)
                .ToListAsync();

            // Hent unikke sagsnumre (ikke-tomme)
            Sagsnumre = await _context.Registreringer
                .Where(r => !string.IsNullOrEmpty(r.Sagsnummer))
                .Select(r => r.Sagsnummer!)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();
        }

        public string GetSortUrl(string column)
        {
            var descending = (SortBy == column) ? !SortDescending : (column == "Dato");

            var queryParams = new List<string>();

            if (FraDato.HasValue) queryParams.Add($"FraDato={FraDato.Value:yyyy-MM-dd}");
            if (TilDato.HasValue) queryParams.Add($"TilDato={TilDato.Value:yyyy-MM-dd}");
            if (!string.IsNullOrEmpty(ValgtAfdeling)) queryParams.Add($"ValgtAfdeling={Uri.EscapeDataString(ValgtAfdeling)}");
            if (!string.IsNullOrEmpty(ValgtBruger)) queryParams.Add($"ValgtBruger={Uri.EscapeDataString(ValgtBruger)}");
            // NYE PARAMETRE
            if (!string.IsNullOrEmpty(ValgtOuAfdeling)) queryParams.Add($"ValgtOuAfdeling={Uri.EscapeDataString(ValgtOuAfdeling)}");
            if (!string.IsNullOrEmpty(ValgtSagsnummer)) queryParams.Add($"ValgtSagsnummer={Uri.EscapeDataString(ValgtSagsnummer)}");

            queryParams.Add($"SortBy={column}");
            queryParams.Add($"SortDescending={descending}");

            return $"/Oversigt?{string.Join("&", queryParams)}";
        }

        public string GetSortIcon(string column)
        {
            if (SortBy != column) return "bi-arrow-down-up";
            return SortDescending ? "bi-sort-down" : "bi-sort-up";
        }
    }
}