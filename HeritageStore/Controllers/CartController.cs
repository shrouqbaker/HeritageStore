using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeritageStore.Data;
using HeritageStore.Models;

namespace HeritageStore.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private const decimal FlatDeliveryCost = 5.0m;

        public CartController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Cart
        public IActionResult Index()
        {
            return View();
        }

        // GET: /Cart/GetCartData (للمستخدم المسجل)
        [HttpGet]
        public async Task<IActionResult> GetCartData()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new
                {
                    success = true,
                    items = new List<object>(),
                    subtotal = 0m,
                    deliveryCost = FlatDeliveryCost,
                    total = 0m,
                    count = 0
                });
            }

            var cartItems = await _context.CartItems
                .Include(c => c.Product)
                    .ThenInclude(p => p.ProductImages)
                .Include(c => c.Product)
                    .ThenInclude(p => p.Category)
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.AddedAt)
                .ToListAsync();

            var items = cartItems.Select(c =>
            {
                var p = c.Product;
                var mainImg = p?.ProductImages?.FirstOrDefault(i => i.IsMain) ?? p?.ProductImages?.FirstOrDefault();
                bool isSold = p?.Status == "sold";
                bool isAvailable = p != null && p.ListingType == "for_sale" && p.Status == "approved";

                return new
                {
                    productId = c.ProductId,
                    name = p?.Name ?? "قطعة غير متوفرة",
                    price = p?.Price ?? 0m,
                    listingType = p?.ListingType ?? "for_sale",
                    status = p?.Status ?? "unknown",
                    isSold = isSold,
                    isAvailable = isAvailable,
                    categoryName = p?.Category?.Name ?? "",
                    country = p?.Country ?? "",
                    imageUrl = mainImg?.ImageUrl ?? "/images/placeholder.jpg"
                };
            }).ToList();

            var availableItems = items.Where(i => i.isAvailable).ToList();
            var subtotal = availableItems.Sum(i => i.price);
            var total = availableItems.Any() ? subtotal + FlatDeliveryCost : 0m;

            return Json(new
            {
                success = true,
                items = items,
                subtotal = subtotal,
                deliveryCost = FlatDeliveryCost,
                total = total,
                count = items.Count
            });
        }

        // POST: /Cart/GetGuestCartData (للزائر: يستقبل مصفوفة IDs من LocalStorage)
        [HttpPost]
        public async Task<IActionResult> GetGuestCartData([FromBody] List<int> productIds)
        {
            if (productIds == null || !productIds.Any())
            {
                return Json(new
                {
                    success = true,
                    items = new List<object>(),
                    subtotal = 0m,
                    deliveryCost = FlatDeliveryCost,
                    total = 0m,
                    count = 0
                });
            }

            var distinctIds = productIds.Distinct().ToList();

            var products = await _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.Category)
                .Where(p => distinctIds.Contains(p.ProductId))
                .ToListAsync();

            var productMap = products.ToDictionary(p => p.ProductId);

            var items = new List<object>();
            decimal subtotal = 0m;
            int availableCount = 0;

            foreach (var id in distinctIds)
            {
                if (productMap.TryGetValue(id, out var p))
                {
                    var mainImg = p.ProductImages?.FirstOrDefault(i => i.IsMain) ?? p.ProductImages?.FirstOrDefault();
                    bool isSold = p.Status == "sold";
                    bool isAvailable = p.ListingType == "for_sale" && p.Status == "approved";

                    if (isAvailable && p.Price.HasValue)
                    {
                        subtotal += p.Price.Value;
                        availableCount++;
                    }

                    items.Add(new
                    {
                        productId = p.ProductId,
                        name = p.Name,
                        price = p.Price ?? 0m,
                        listingType = p.ListingType,
                        status = p.Status,
                        isSold = isSold,
                        isAvailable = isAvailable,
                        categoryName = p.Category?.Name ?? "",
                        country = p.Country,
                        imageUrl = mainImg?.ImageUrl ?? "/images/placeholder.jpg"
                    });
                }
            }

            var total = availableCount > 0 ? subtotal + FlatDeliveryCost : 0m;

            return Json(new
            {
                success = true,
                items = items,
                subtotal = subtotal,
                deliveryCost = FlatDeliveryCost,
                total = total,
                count = items.Count
            });
        }

        // POST: /Cart/AddToCart
        [HttpPost]
        public async Task<IActionResult> AddToCart([FromBody] CartActionModel model)
        {
            if (model == null || model.ProductId <= 0)
            {
                return BadRequest(new { success = false, message = "معرّف المنتج غير صالح" });
            }

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = true, isGuest = true });
            }

            var product = await _context.Products.FindAsync(model.ProductId);
            if (product == null || product.ListingType != "for_sale" || product.Status != "approved")
            {
                return Json(new { success = false, message = "هذه القطعة غير متاحة للشراء حالياً" });
            }

            bool alreadyInCart = await _context.CartItems.AnyAsync(c => c.UserId == userId && c.ProductId == model.ProductId);
            if (!alreadyInCart)
            {
                _context.CartItems.Add(new CartItem
                {
                    UserId = userId,
                    ProductId = model.ProductId,
                    AddedAt = DateTime.Now
                });
                await _context.SaveChangesAsync();
            }

            var cartCount = await _context.CartItems.CountAsync(c => c.UserId == userId);
            return Json(new { success = true, message = "تمت إضافة القطعة إلى سلتك", count = cartCount });
        }

        // POST: /Cart/RemoveFromCart
        [HttpPost]
        public async Task<IActionResult> RemoveFromCart([FromBody] CartActionModel model)
        {
            if (model == null || model.ProductId <= 0)
            {
                return BadRequest(new { success = false, message = "معرّف المنتج غير صالح" });
            }

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = true, isGuest = true });
            }

            var cartItem = await _context.CartItems.FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == model.ProductId);
            if (cartItem != null)
            {
                _context.CartItems.Remove(cartItem);
                await _context.SaveChangesAsync();
            }

            var cartCount = await _context.CartItems.CountAsync(c => c.UserId == userId);
            return Json(new { success = true, message = "تم حذف القطعة من السلة", count = cartCount });
        }

        // POST: /Cart/MergeGuestCart (يُستدعى تلقائيًا بعد تسجيل الدخول)
        [HttpPost]
        public async Task<IActionResult> MergeGuestCart([FromBody] List<int> productIds)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId) || productIds == null || !productIds.Any())
            {
                return Json(new { success = true });
            }

            var existingProductIds = await _context.CartItems
                .Where(c => c.UserId == userId)
                .Select(c => c.ProductId)
                .ToListAsync();

            var distinctNewIds = productIds.Distinct().Where(id => !existingProductIds.Contains(id)).ToList();

            if (distinctNewIds.Any())
            {
                var validProducts = await _context.Products
                    .Where(p => distinctNewIds.Contains(p.ProductId) && p.ListingType == "for_sale")
                    .Select(p => p.ProductId)
                    .ToListAsync();

                foreach (var productId in validProducts)
                {
                    _context.CartItems.Add(new CartItem
                    {
                        UserId = userId,
                        ProductId = productId,
                        AddedAt = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();
            }

            var count = await _context.CartItems.CountAsync(c => c.UserId == userId);
            return Json(new { success = true, count = count });
        }

        // GET: /Cart/GetCartCount
        [HttpGet]
        public async Task<IActionResult> GetCartCount()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { count = 0 });
            }

            var count = await _context.CartItems.CountAsync(c => c.UserId == userId);
            return Json(new { count = count });
        }
    }

    public class CartActionModel
    {
        public int ProductId { get; set; }
    }
}