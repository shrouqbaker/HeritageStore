using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeritageStore.Data;
using HeritageStore.Models;
using HeritageStore.Models.ViewModels;

namespace HeritageStore.Controllers
{
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IWebHostEnvironment _env;

        public ProfileController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _env = env;
        }

        // GET: /Profile
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var userId = user.Id;

            // جلب المنتجات والطلبات لعرضها مباشرة داخل تبويبات نفس الصفحة
            var userProducts = await _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.Category)
                .Include(p => p.EmbroideryType)
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var userOrders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.ProductImages)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            var totalFavorites = await _context.Favorites.CountAsync(f => f.UserId == userId);

            // معرّفات المفضلة لتلوين الأزرار في معاينة الملف العام
            var favoriteIds = (await _context.Favorites
                .Where(f => f.UserId == userId)
                .Select(f => f.ProductId)
                .ToListAsync()).ToHashSet();
            ViewBag.FavoriteIds = favoriteIds;

            var vm = new ProfileEditViewModel
            {
                Id = user.Id,
                UserName = user.UserName ?? "",
                Email = user.Email ?? "",
                PhoneNumber = user.PhoneNumber,
                Bio = user.Bio,
                ProfileImage = user.ProfileImage,
                CreatedAt = user.CreatedAt,
                TotalProducts = userProducts.Count,
                TotalOrders = userOrders.Count,
                TotalFavorites = totalFavorites,
                Products = userProducts,
                Orders = userOrders
            };

            return View(vm);
        }

        // POST: /Profile
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ProfileEditViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            // إتاحة تعديل اسم المستخدم مع التحقق من عدم تكراره
            if (!string.IsNullOrWhiteSpace(model.UserName) && !string.Equals(model.UserName, user.UserName, StringComparison.OrdinalIgnoreCase))
            {
                var existing = await _userManager.FindByNameAsync(model.UserName);
                if (existing != null && existing.Id != user.Id)
                {
                    ModelState.AddModelError("UserName", "اسم المستخدم هذا مستخدم بالفعل، الرجاء اختيار اسم آخر.");
                }
                else
                {
                    user.UserName = model.UserName;
                }
            }

            if (!ModelState.IsValid)
            {
                model.Email = user.Email ?? "";
                model.ProfileImage = user.ProfileImage;
                model.CreatedAt = user.CreatedAt;

                model.Products = await _context.Products
                    .Include(p => p.ProductImages)
                    .Include(p => p.Category)
                    .Include(p => p.EmbroideryType)
                    .Where(p => p.UserId == user.Id)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();

                model.Orders = await _context.Orders
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                            .ThenInclude(p => p.ProductImages)
                    .Where(o => o.UserId == user.Id)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();

                model.TotalProducts = model.Products.Count;
                model.TotalOrders = model.Orders.Count;
                model.TotalFavorites = await _context.Favorites.CountAsync(f => f.UserId == user.Id);

                return View(model);
            }

            user.PhoneNumber = model.PhoneNumber;
            user.Bio = model.Bio;

            // تحديث المستخدم وتجديد جلسة تسجيل الدخول ليعكس الاسم الجديد فورياً في الموقع
            await _userManager.UpdateAsync(user);
            await _signInManager.RefreshSignInAsync(user);

            TempData["SuccessMessage"] = "تم حفظ التعديلات بنجاح!";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Profile/UpdateAvatar (رفع وتحديث الصورة الشخصية فورياً عبر AJAX دون الحاجة للضغط على حفظ)
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> UpdateAvatar(IFormFile avatar)
        {
            if (avatar == null || avatar.Length == 0)
            {
                return Json(new { success = false, message = "لم يتم اختيار أي صورة." });
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(avatar.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                return Json(new { success = false, message = "نوع الصورة غير مدعوم. يرجى اختيار ملف JPG أو PNG أو WEBP." });
            }

            if (avatar.Length > 5 * 1024 * 1024)
            {
                return Json(new { success = false, message = "حجم الصورة كبير جداً، الحد الأقصى 5 ميجابايت." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { success = false, message = "المستخدم غير مسجل." });
            }

            string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "profiles");
            Directory.CreateDirectory(uploadsFolder);

            string uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(avatar.FileName)}";
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await avatar.CopyToAsync(stream);
            }

            user.ProfileImage = $"/uploads/profiles/{uniqueFileName}";
            await _userManager.UpdateAsync(user);

            return Json(new
            {
                success = true,
                imageUrl = user.ProfileImage,
                message = "تم تحديث الصورة الشخصية وحفظها فورياً بنجاح!"
            });
        }

        // POST: /Profile/DeleteProduct (حذف ناعم للقطعة التراثية: IsDeleted = true)
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                          Request.Headers.Accept.ToString().Contains("application/json");

            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == id && p.UserId == user.Id);
            if (product == null)
            {
                if (isAjax)
                {
                    return Json(new { success = false, message = "القطعة غير موجودة أو تم حذفها مسبقاً." });
                }
                TempData["ErrorMessage"] = "القطعة غير موجودة أو ليس لديك صلاحية لحذفها.";
                return RedirectToAction(nameof(Index));
            }

            product.IsDeleted = true;
            product.DeletedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            if (isAjax)
            {
                var remainingCount = await _context.Products.CountAsync(p => p.UserId == user.Id && !p.IsDeleted);
                return Json(new { success = true, message = "تم حذف القطعة بنجاح.", remainingCount });
            }

            TempData["SuccessMessage"] = "تم حذف القطعة بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Profile/User/{id} أو /Profile/Details/{id} (متاح لجميع المستخدمين والزوار)
        [AllowAnonymous]
        [HttpGet("Profile/User/{id}")]
        [HttpGet("Profile/Details/{id}")]
        public async Task<IActionResult> UserProfile(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (targetUser == null)
            {
                return NotFound();
            }

            // جلب القطع العامة فقط (المعتمدة، المباعة، أو المعروضة للتراث)
            var products = await _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.EmbroideryType)
                .Include(p => p.Category)
                .Where(p => p.UserId == id && (p.Status == "approved" || p.Status == "sold" || p.Status == "archived"))
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            // معرفات المفضلة للمستخدم المسجل الحالي لتلوين القلوب
            var currentUserId = _userManager.GetUserId(User);
            var favoriteIds = new HashSet<int>();
            if (!string.IsNullOrEmpty(currentUserId))
            {
                favoriteIds = (await _context.Favorites
                    .Where(f => f.UserId == currentUserId)
                    .Select(f => f.ProductId)
                    .ToListAsync()).ToHashSet();
            }
            ViewBag.FavoriteIds = favoriteIds;

            var vm = new PublicProfileViewModel
            {
                User = targetUser,
                Products = products,
                TotalProducts = products.Count,
                TotalForSale = products.Count(p => p.ListingType == "for_sale"),
                TotalArchived = products.Count(p => p.ListingType == "archive_only")
            };

            return View("PublicProfile", vm);
        }
    }
}
