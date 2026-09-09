using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeritageStore.Data;
using HeritageStore.Models;

namespace HeritageStore.Controllers
{
    public class FavoriteController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public FavoriteController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Favorite
        public IActionResult Index()
        {
            return View();
        }

        // GET: /Favorite/GetFavoriteData (للمستخدم المسجل)
        [HttpGet]
        public async Task<IActionResult> GetFavoriteData()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = true, items = new List<object>(), count = 0 });
            }

            var favorites = await _context.Favorites
                .Include(f => f.Product)
                    .ThenInclude(p => p.ProductImages)
                .Include(f => f.Product)
                    .ThenInclude(p => p.EmbroideryType)
                .Include(f => f.Product)
                    .ThenInclude(p => p.Category)
                .Where(f => f.UserId == userId)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            var items = favorites.Select(f =>
            {
                var p = f.Product;
                var mainImg = p?.ProductImages?.FirstOrDefault(i => i.IsMain) ?? p?.ProductImages?.FirstOrDefault();

                return new
                {
                    productId = f.ProductId,
                    name = p?.Name ?? "قطعة غير متوفرة",
                    price = p?.Price,
                    listingType = p?.ListingType ?? "for_sale",
                    status = p?.Status ?? "unknown",
                    isSold = p?.Status == "sold",
                    categoryName = p?.Category?.Name ?? "",
                    country = p?.Country ?? "",
                    embroideryName = p?.EmbroideryType?.Name ?? "",
                    imageUrl = mainImg?.ImageUrl ?? "/images/placeholder.jpg"
                };
            }).ToList();

            return Json(new { success = true, items = items, count = items.Count });
        }

        // POST: /Favorite/GetGuestFavoriteData (للزائر: يستقبل مصفوفة IDs من LocalStorage)
        [HttpPost]
        public async Task<IActionResult> GetGuestFavoriteData([FromBody] List<int> productIds)
        {
            if (productIds == null || !productIds.Any())
            {
                return Json(new { success = true, items = new List<object>(), count = 0 });
            }

            var distinctIds = productIds.Distinct().ToList();

            var products = await _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.EmbroideryType)
                .Include(p => p.Category)
                .Where(p => distinctIds.Contains(p.ProductId))
                .ToListAsync();

            var productMap = products.ToDictionary(p => p.ProductId);

            var items = new List<object>();
            foreach (var id in distinctIds)
            {
                if (productMap.TryGetValue(id, out var p))
                {
                    var mainImg = p.ProductImages?.FirstOrDefault(i => i.IsMain) ?? p.ProductImages?.FirstOrDefault();

                    items.Add(new
                    {
                        productId = p.ProductId,
                        name = p.Name,
                        price = p.Price,
                        listingType = p.ListingType,
                        status = p.Status,
                        isSold = p.Status == "sold",
                        categoryName = p.Category?.Name ?? "",
                        country = p.Country,
                        embroideryName = p.EmbroideryType?.Name ?? "",
                        imageUrl = mainImg?.ImageUrl ?? "/images/placeholder.jpg"
                    });
                }
            }

            return Json(new { success = true, items = items, count = items.Count });
        }

        // POST: /Favorite/ToggleFavorite
        [HttpPost]
        public async Task<IActionResult> ToggleFavorite([FromBody] FavoriteActionModel model)
        {
            if (model == null || model.ProductId <= 0)
            {
                return BadRequest(new { success = false, message = "معرّف المنتج غير صالح" });
            }

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                // إذا كان زائر يتم التعامل معها في الواجهة عبر LocalStorage
                return Json(new { success = true, isGuest = true });
            }

            var existing = await _context.Favorites.FirstOrDefaultAsync(f => f.UserId == userId && f.ProductId == model.ProductId);
            bool isFavorited;

            if (existing != null)
            {
                _context.Favorites.Remove(existing);
                await _context.SaveChangesAsync();
                isFavorited = false;
            }
            else
            {
                var productExists = await _context.Products.AnyAsync(p => p.ProductId == model.ProductId);
                if (!productExists)
                {
                    return Json(new { success = false, message = "القطعة غير موجودة" });
                }

                _context.Favorites.Add(new Favorite
                {
                    UserId = userId,
                    ProductId = model.ProductId,
                    CreatedAt = DateTime.Now
                });
                await _context.SaveChangesAsync();
                isFavorited = true;
            }

            return Json(new
            {
                success = true,
                isFavorited = isFavorited,
                message = isFavorited ? "تمت إضافة القطعة إلى المفضلة" : "تمت إزالة القطعة من المفضلة"
            });
        }

        // POST: /Favorite/MergeGuestFavorites (يُستدعى تلقائيًا بعد تسجيل الدخول)
        [HttpPost]
        public async Task<IActionResult> MergeGuestFavorites([FromBody] List<int> productIds)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId) || productIds == null || !productIds.Any())
            {
                return Json(new { success = true });
            }

            var existingProductIds = await _context.Favorites
                .Where(f => f.UserId == userId)
                .Select(f => f.ProductId)
                .ToListAsync();

            var distinctNewIds = productIds.Distinct().Where(id => !existingProductIds.Contains(id)).ToList();

            if (distinctNewIds.Any())
            {
                var validProducts = await _context.Products
                    .Where(p => distinctNewIds.Contains(p.ProductId))
                    .Select(p => p.ProductId)
                    .ToListAsync();

                foreach (var productId in validProducts)
                {
                    _context.Favorites.Add(new Favorite
                    {
                        UserId = userId,
                        ProductId = productId,
                        CreatedAt = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();
            }

            return Json(new { success = true });
        }

        // GET: /Favorite/GetFavoriteIds (للمزامنة في الواجهة)
        [HttpGet]
        public async Task<IActionResult> GetFavoriteIds()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { favoriteIds = new List<int>() });
            }

            var ids = await _context.Favorites
                .Where(f => f.UserId == userId)
                .Select(f => f.ProductId)
                .ToListAsync();

            return Json(new { favoriteIds = ids });
        }
    }

    public class FavoriteActionModel
    {
        public int ProductId { get; set; }
    }
}