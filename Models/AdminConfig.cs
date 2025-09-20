using Microsoft.EntityFrameworkCore;
using Tidsregistrering.Data;

namespace Tidsregistrering.Models
{
    public static class AdminConfig
    {
        // Fallback administrator (i tilfælde af database problemer)
        public static readonly string FallbackAdmin = "IBK\\chrijen"; // <-- FJERN .LAN

        // Check om en bruger er administrator via database
        public static async Task<bool> IsAdminAsync(System.Security.Claims.ClaimsPrincipal user, TidsregistreringContext context)
        {
            if (user?.Identity?.Name == null)
                return false;

            var username = user.Identity.Name;

            try
            {
                // Check i database
                var isAdmin = await context.Administratorer
                    .AnyAsync(a => a.Brugernavn.ToLower() == username.ToLower() && a.Aktiv);

                // Fallback til hardkodet admin hvis database fejler eller er tom
                if (!isAdmin && username.Equals(FallbackAdmin, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return isAdmin;
            }
            catch
            {
                // Database fejl - fallback til hardkodet admin
                return username.Equals(FallbackAdmin, StringComparison.OrdinalIgnoreCase);
            }
        }

        // Synkron version til navigation (mindre pålidelig, men nødvendig for _Layout)
        public static bool IsAdminSync(System.Security.Claims.ClaimsPrincipal user, TidsregistreringContext context)
        {
            if (user?.Identity?.Name == null)
                return false;

            var username = user.Identity.Name;

            try
            {
                // Check i database
                var isAdmin = context.Administratorer
                    .Any(a => a.Brugernavn.ToLower() == username.ToLower() && a.Aktiv);

                // Fallback til hardkodet admin
                if (!isAdmin && username.Equals(FallbackAdmin, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return isAdmin;
            }
            catch
            {
                // Database fejl - fallback til hardkodet admin
                return username.Equals(FallbackAdmin, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}