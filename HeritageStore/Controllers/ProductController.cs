using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeritageStore.Data;
using HeritageStore.Models;
using HeritageStore.Models.ViewModels;

namespace HeritageStore.Controllers
{
    [Authorize]
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public ProductController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
        }

        [HttpGet]
        public async Task<IActionResult> Add()
        {
            var vm = new ProductFormViewModel
            {
                CategoryList = await _context.Categories.OrderBy(c => c.Name).ToListAsync(),
                EmbroideryTypeList = await _context.EmbroideryTypes.OrderBy(e => e.Name).ToListAsync()
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(ProductFormViewModel vm)
        {
            // تحقق: لازم صورة وحدة ع الأقل
            if (vm.Images == null || vm.Images.Count == 0 || vm.Images.All(f => f.Length == 0))
            {
                ModelState.AddModelError("Images", "لازم ترفع صورة واحدة على الأقل للقطعة");
            }

            // تحقق: كل تطريزة معبأة يجب أن تحتوي صورة + اسم + وصف معًا
            var validEmbroideries = new List<EmbroideryInputModel>();
            for (int i = 0; i < vm.Embroideries.Count; i++)
            {
                var emb = vm.Embroideries[i];
                bool hasName = !string.IsNullOrWhiteSpace(emb.Name);
                bool hasDescription = !string.IsNullOrWhiteSpace(emb.Description);
                bool hasImage = emb.Image != null && emb.Image.Length > 0;

                bool isCompletelyEmpty = !hasName && !hasDescription && !hasImage;
                bool isFullyFilled = hasName && hasDescription && hasImage;

                if (isCompletelyEmpty)
                {
                    continue; // تجاهل التطريزة الفاضية بالكامل، عادي
                }

                if (!isFullyFilled)
                {
                    ModelState.AddModelError("", $"تطريزة {i + 1}: يجب تعبئة الصورة والاسم والوصف معًا، أو تركها فارغة بالكامل");
                }
                else
                {
                    validEmbroideries.Add(emb);
                }
            }

            // تحقق: لو للبيع، السعر إلزامي
            if (vm.ListingType == "for_sale" && (!vm.Price.HasValue || vm.Price <= 0))
            {
                ModelState.AddModelError("Price", "السعر مطلوب عند اختيار البيع");
            }

            if (!ModelState.IsValid)
            {
                vm.CategoryList = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
                vm.EmbroideryTypeList = await _context.EmbroideryTypes.OrderBy(e => e.Name).ToListAsync();
                return View(vm);
            }

            var userId = _userManager.GetUserId(User);

            var product = new Product
            {
                UserId = userId!,
                CategoryId = vm.CategoryId,
                EmbroideryTypeId = vm.EmbroideryTypeId,
                Country = vm.Country,
                Name = vm.Name,
                Description = vm.Description,
                Story = vm.Story,
                HistoricalBackground = vm.HistoricalBackground,
                Occasion = vm.Occasion,
                AdditionalInfo = vm.AdditionalInfo,
                PickupAddress = vm.PickupAddress,
                ListingType = vm.ListingType,
                Price = vm.ListingType == "for_sale" ? vm.Price : null,
                Status = "pending",
                CreatedAt = DateTime.Now
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            // حفظ صور المنتج
            string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "products");
            Directory.CreateDirectory(uploadsFolder);

            for (int i = 0; i < vm.Images.Count; i++)
            {
                var file = vm.Images[i];
                if (file.Length == 0) continue;

                string fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                string filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                _context.ProductImages.Add(new ProductImage
                {
                    ProductId = product.ProductId,
                    ImageUrl = $"/uploads/products/{fileName}",
                    IsMain = (i == vm.MainImageIndex)
                });
            }

            // حفظ التطريزات
            foreach (var emb in validEmbroideries)
            {
                string embImageUrl = "/images/placeholder.jpg";

                if (emb.Image != null && emb.Image.Length > 0)
                {
                    string embFileName = $"{Guid.NewGuid()}_{Path.GetFileName(emb.Image.FileName)}";
                    string embFilePath = Path.Combine(uploadsFolder, embFileName);

                    using (var stream = new FileStream(embFilePath, FileMode.Create))
                    {
                        await emb.Image.CopyToAsync(stream);
                    }
                    embImageUrl = $"/uploads/products/{embFileName}";
                }

                _context.Embroideries.Add(new Embroidery
                {
                    ProductId = product.ProductId,
                    Name = emb.Name!,
                    Description = emb.Description ?? "",
                    ImageUrl = embImageUrl
                });
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إرسال قطعتك بنجاح! بانتظار مراجعة الإدارة قبل النشر.";
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public async Task<IActionResult> MyProducts(string status = "all")
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            var allUserProducts = await _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.Category)
                .Include(p => p.EmbroideryType)
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var query = allUserProducts.AsEnumerable();

            switch (status?.ToLower())
            {
                case "approved":
                    query = query.Where(p => p.Status == "approved");
                    break;
                case "pending":
                    query = query.Where(p => p.Status == "pending");
                    break;
                case "sold":
                    query = query.Where(p => p.Status == "sold");
                    break;
                case "rejected":
                    query = query.Where(p => p.Status == "rejected");
                    break;
                case "archive_only":
                    query = query.Where(p => p.ListingType == "archive_only");
                    break;
                default:
                    status = "all";
                    break;
            }

            var vm = new MyProductsViewModel
            {
                Products = query.ToList(),
                CurrentStatus = status ?? "all",
                TotalCount = allUserProducts.Count,
                ApprovedCount = allUserProducts.Count(p => p.Status == "approved"),
                PendingCount = allUserProducts.Count(p => p.Status == "pending"),
                SoldCount = allUserProducts.Count(p => p.Status == "sold"),
                RejectedCount = allUserProducts.Count(p => p.Status == "rejected"),
                ArchivedCount = allUserProducts.Count(p => p.ListingType == "archive_only")
            };

            return View(vm);
        }

        // POST: /Product/Delete (حذف ناعم للقطعة التراثية: IsDeleted = true)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
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
                return RedirectToAction("Index", "Profile");
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
            return RedirectToAction("Index", "Profile");
        }

        [AllowAnonymous]
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.Embroideries)
                .Include(p => p.EmbroideryType)
                .Include(p => p.Category)
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null)
            {
                return NotFound();
            }

            // منتجات مشابهة (نفس التصنيف أو نفس الدولة، عدا المنتج الحالي)
            var similarProducts = await _context.Products
                .Include(p => p.ProductImages).Include(p => p.Embroideries)
                .Include(p => p.EmbroideryType)
                .Where(p => p.ProductId != id &&
                            (p.CategoryId == product.CategoryId || p.Country == product.Country) &&
                            (p.Status == "sold" || p.Status == "approved" || p.Status == "archived"))
                .OrderByDescending(p => p.CreatedAt)
                .Take(4)
                .ToListAsync();

            // هل المنتج مفضّل عند المستخدم الحالي؟
            bool isFavorited = false;
            var userId = _userManager.GetUserId(User);
            if (userId != null)
            {
                isFavorited = await _context.Favorites.AnyAsync(f => f.UserId == userId && f.ProductId == id);
            }

            ViewBag.SimilarProducts = similarProducts;
            ViewBag.IsFavorited = isFavorited;

            return View(product);
        }
    }
}