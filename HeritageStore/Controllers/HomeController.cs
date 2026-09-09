using System.Diagnostics;
using HeritageStore.Data;
using HeritageStore.Models;
using HeritageStore.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace HeritageStore.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;


        public HomeController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var favoriteIds = new HashSet<int>();

            if (userId != null)
            {
                favoriteIds = (await _context.Favorites
                    .Where(f => f.UserId == userId)
                    .Select(f => f.ProductId)
                    .ToListAsync()).ToHashSet();
            }

            ViewBag.FavoriteIds = favoriteIds;

            var viewModel = new HomeViewModel
            {
                Categories = await _context.Categories
                 .OrderBy(c => c.CategoryId)
                 .Take(4)
                 .ToListAsync(),

                LatestProducts = await _context.Products
                 .Include(p => p.ProductImages)
                 .Include(p => p.EmbroideryType)
                 .Where(p => p.Status == "sold" ||
                             p.Status == "approved" ||
                             p.Status == "archived")
                 .OrderByDescending(p => p.CreatedAt)
                 .Take(4)
                 .ToListAsync(),

                ForSaleProducts = await _context.Products
                    .CountAsync(p => p.ListingType == "for_sale" &&
                                     p.Price.HasValue &&
                                     p.Status == "approved"),

                ArchivedProducts = await _context.Products
                     .CountAsync(p => p.ListingType == "archive_only" || (p.ListingType == "for_sale" && p.Status == "sold")),

                TotalCategories = await _context.Categories
                   .CountAsync(),

                TotalUsers = await _context.Users
                     .CountAsync()
            };
            return View(viewModel);
        }

        public IActionResult About()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
