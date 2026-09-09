using HeritageStore.Data;
using HeritageStore.Models;
using HeritageStore.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeritageStore.Controllers
{
    public class ExploreController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private const int PageSize = 12;

        public ExploreController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(
            int? categoryId,
            string? country,
            List<int>? embroideryTypeIds,
            string? listingType,
            string? search,
            int page = 1)
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
            var query = _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.EmbroideryType)
                .Include(p => p.Category)
                .Where(p => p.Status == "sold" || p.Status == "approved" || p.Status == "archived")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.Name.Contains(search) || p.Description.Contains(search) || p.EmbroideryType.Name.Contains(search));

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId.Value);

            if (!string.IsNullOrEmpty(country))
                query = query.Where(p => p.Country == country);

            if (embroideryTypeIds != null && embroideryTypeIds.Any())
                query = query.Where(p => p.EmbroideryTypeId.HasValue && embroideryTypeIds.Contains(p.EmbroideryTypeId.Value));

            if (!string.IsNullOrEmpty(listingType))
                query = query.Where(p => p.ListingType == listingType);

            int totalCount = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

            var products = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            var vm = new ExploreViewModel
            {
                Products = products,
                Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync(),
                EmbroideryTypes = await _context.EmbroideryTypes.OrderBy(e => e.Name).ToListAsync(),
                CategoryId = categoryId,
                Country = country,
                EmbroideryTypeIds = embroideryTypeIds ?? new List<int>(),
                ListingType = listingType,
                SearchTerm = search,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalCount = totalCount
            };

            return View(vm);
        }
    }
}