using Microsoft.AspNetCore.Mvc;
using Tidsregistrering.Data;
using Tidsregistrering.Models;

namespace Tidsregistrering.ViewComponents
{
    public class AdminNavViewComponent : ViewComponent
    {
        private readonly TidsregistreringContext _context;

        public AdminNavViewComponent(TidsregistreringContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var isAdmin = await AdminConfig.IsAdminAsync(HttpContext.User, _context);
            var currentPath = HttpContext.Request.Path;

            var model = new AdminNavViewModel
            {
                IsAdmin = isAdmin,
                IsCurrentPage = currentPath == "/Admin"
            };

            return View(model);
        }
    }

    public class AdminNavViewModel
    {
        public bool IsAdmin { get; set; }
        public bool IsCurrentPage { get; set; }
    }
}