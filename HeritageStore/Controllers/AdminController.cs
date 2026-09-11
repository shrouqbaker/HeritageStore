using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeritageStore.Data;
using HeritageStore.Models;
using HeritageStore.Models.ViewModels;

namespace HeritageStore.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
        }

        // GET: /Admin أو /Admin/Index (Dashboard)
        public async Task<IActionResult> Index()
        {
            var totalProducts = await _context.Products.CountAsync();
            var totalOrders = await _context.Orders.CountAsync();
            var totalUsers = await _userManager.Users.CountAsync();
            var totalSales = await _context.Orders
                .Where(o => o.Status == "delivered" || o.Status == "completed" || o.Status == "shipped" || o.Status == "processing")
                .SumAsync(o => (decimal?)o.TotalPrice) ?? 0;

            var pendingProductsCount = await _context.Products.CountAsync(p => p.Status == "pending");
            var approvedProductsCount = await _context.Products.CountAsync(p => p.Status == "approved");
            var soldCount = await _context.Products.CountAsync(p => p.Status == "sold");
            var rejectedProductsCount = await _context.Products.CountAsync(p => p.Status == "rejected");
            var archivedProductsCount = await _context.Products.CountAsync(p => p.ListingType == "archive_only");

            // أحدث الطلبات
            var recentOrders = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .OrderByDescending(o => o.CreatedAt)
                .Take(5)
                .ToListAsync();

            // قطع بانتظار المراجعة والاعتماد
            var pendingProducts = await _context.Products
                .Include(p => p.User)
                .Include(p => p.Category)
                .Include(p => p.ProductImages)
                .Where(p => p.Status == "pending")
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .ToListAsync();

            // أفضل التصنيفات من حيث عدد المنتجات والمبيعات
            var categories = await _context.Categories
                .Include(c => c.Products)
                .ToListAsync();

            var topCategories = categories.Select(c => new TopCategoryStat
            {
                CategoryName = c.Name,
                Count = c.Products.Count,
                TotalSales = c.Products.Where(p => p.Price.HasValue).Sum(p => p.Price!.Value),
                Percentage = totalProducts > 0 ? (int)Math.Round((double)c.Products.Count / totalProducts * 100) : 0
            }).OrderByDescending(c => c.Count).Take(5).ToList();

            // حركة المبيعات والطلبات خلال آخر 6 أشهر
            var monthlyTrend = new List<MonthlySalesTrend>();
            var arabicMonths = new[] { "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو", "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر" };

            var now = DateTime.Now;
            for (int i = 5; i >= 0; i--)
            {
                var targetDate = now.AddMonths(-i);
                var year = targetDate.Year;
                var month = targetDate.Month;

                var monthOrders = await _context.Orders
                    .Where(o => o.CreatedAt.Year == year && o.CreatedAt.Month == month)
                    .ToListAsync();

                monthlyTrend.Add(new MonthlySalesTrend
                {
                    MonthName = arabicMonths[month - 1],
                    Sales = monthOrders.Sum(o => o.TotalPrice),
                    OrdersCount = monthOrders.Count
                });
            }

            var vm = new AdminDashboardViewModel
            {
                TotalProducts = totalProducts,
                TotalOrders = totalOrders,
                TotalSales = totalSales,
                TotalUsers = totalUsers,
                PendingProductsCount = pendingProductsCount,
                ApprovedProductsCount = approvedProductsCount,
                SoldCount = soldCount,
                RejectedProductsCount = rejectedProductsCount,
                ArchivedProductsCount = archivedProductsCount,
                RecentOrders = recentOrders,
                PendingProducts = pendingProducts,
                TopCategories = topCategories,
                MonthlyTrend = monthlyTrend
            };

            return View(vm);
        }

        // GET: /Admin/Products (Product Control)
        public async Task<IActionResult> Products(string status = "all", string? search = null)
        {
            var query = _context.Products
                .Include(p => p.User)
                .Include(p => p.Category)
                .Include(p => p.EmbroideryType)
                .Include(p => p.ProductImages)
                .AsQueryable();

            var allProducts = await query.ToListAsync();

            int totalCount = allProducts.Count;
            int approvedCount = allProducts.Count(p => p.Status == "approved");
            int rejectedCount = allProducts.Count(p => p.Status == "rejected");
            int soldCount = allProducts.Count(p => p.Status == "sold");
            int pendingCount = allProducts.Count(p => p.Status == "pending");

            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                query = query.Where(p => p.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(p => p.Name.Contains(search) ||
                                         (p.User != null && (p.User.UserName!.Contains(search) || p.User.Email!.Contains(search))) ||
                                         (p.Category != null && p.Category.Name.Contains(search)));
            }

            var filteredProducts = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();

            var vm = new AdminProductsViewModel
            {
                Products = filteredProducts,
                CurrentStatus = status,
                SearchTerm = search,
                TotalCount = totalCount,
                ApprovedCount = approvedCount,
                RejectedCount = rejectedCount,
                SoldCount = soldCount,
                PendingCount = pendingCount
            };

            return View(vm);
        }

        // POST: /Admin/UpdateProductStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProductStatus(int id, string status, string? reason)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == id);
            if (product == null)
            {
                if (IsAjax()) return Json(new { success = false, message = "القطعة غير موجودة." });
                TempData["ErrorMessage"] = "القطعة غير موجودة.";
                return RedirectToAction(nameof(Products));
            }

            product.Status = status;
            if (status == "rejected")
            {
                product.RejectionReason = string.IsNullOrWhiteSpace(reason) ? "لم تستوفِ القطعة معايير العرض المطلوبة." : reason;
            }
            else
            {
                product.RejectionReason = null;
            }

            product.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            string message = status switch
            {
                "approved" => "تم اعتماد ونشر القطعة بنجاح في المتجر! ✓",
                "rejected" => "تم رفض القطعة وحفظ سبب الرفض.",
                "sold" => "تم تمييز القطعة كمباعة بنجاح.",
                _ => "تم تحديث حالة القطعة بنجاح."
            };

            if (IsAjax())
            {
                return Json(new { success = true, message, newStatus = status });
            }

            TempData["SuccessMessage"] = message;
            return RedirectToAction(nameof(Products));
        }

        // POST: /Admin/DeleteProduct (حذف ناعم IsDeleted = true)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == id);
            if (product == null)
            {
                if (IsAjax()) return Json(new { success = false, message = "القطعة غير موجودة." });
                TempData["ErrorMessage"] = "القطعة غير موجودة.";
                return RedirectToAction(nameof(Products));
            }

            product.IsDeleted = true;
            product.DeletedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            if (IsAjax())
            {
                return Json(new { success = true, message = "تم حذف القطعة بنجاح." });
            }

            TempData["SuccessMessage"] = "تم حذف القطعة بنجاح.";
            return RedirectToAction(nameof(Products));
        }

        // GET: /Admin/Categories (Add & Manage Categories)
        public async Task<IActionResult> Categories()
        {
            var categories = await _context.Categories
                .Include(c => c.Products)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var embroideryTypes = await _context.EmbroideryTypes
                .Include(e => e.Products)
                .OrderBy(e => e.Country).ThenBy(e => e.Name)
                .ToListAsync();

            var vm = new AdminCategoriesViewModel
            {
                Categories = categories.Select(c => new CategoryWithCount
                {
                    Category = c,
                    ProductsCount = c.Products.Count(p => !p.IsDeleted)
                }).ToList(),
                EmbroideryTypes = embroideryTypes.Select(e => new EmbroideryTypeWithCount
                {
                    EmbroideryType = e,
                    ProductsCount = e.Products.Count(p => !p.IsDeleted)
                }).ToList(),
                TotalCategories = categories.Count,
                TotalEmbroideryTypes = embroideryTypes.Count
            };

            return View(vm);
        }

        // POST: /Admin/CreateCategory
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCategory(string name, IFormFile? imageFile, string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["ErrorMessage"] = "يرجى كتابة اسم التصنيف.";
                return RedirectToAction(nameof(Categories));
            }

            string finalImageUrl = imageUrl ?? string.Empty;

            if (imageFile != null && imageFile.Length > 0)
            {
                string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "categories");
                Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(imageFile.FileName)}";
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                finalImageUrl = $"/uploads/categories/{uniqueFileName}";
            }

            if (string.IsNullOrWhiteSpace(finalImageUrl))
            {
                finalImageUrl = "https://images.unsplash.com/photo-1622470953794-aa9c70b0fb9d?w=800";
            }

            var category = new Category
            {
                Name = name.Trim(),
                ImageUrl = finalImageUrl,
                CreatedAt = DateTime.Now,
                IsDeleted = false
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"تمت إضافة تصنيف \"{category.Name}\" بنجاح!";
            return RedirectToAction(nameof(Categories));
        }

        // POST: /Admin/DeleteCategory (حذف ناعم IsDeleted = true)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryId == id);
            if (category == null)
            {
                if (IsAjax()) return Json(new { success = false, message = "التصنيف غير موجود." });
                TempData["ErrorMessage"] = "التصنيف غير موجود.";
                return RedirectToAction(nameof(Categories));
            }

            // حذف ناعم
            category.IsDeleted = true;
            category.DeletedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            if (IsAjax())
            {
                return Json(new { success = true, message = "تم حذف التصنيف بنجاح." });
            }

            TempData["SuccessMessage"] = "تم حذف التصنيف بنجاح.";
            return RedirectToAction(nameof(Categories));
        }

        // POST: /Admin/CreateEmbroideryType
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateEmbroideryType(string name, string country)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["ErrorMessage"] = "يرجى إدخال اسم نوع التطريز.";
                return RedirectToAction(nameof(Categories));
            }

            var exists = await _context.EmbroideryTypes.AnyAsync(e => e.Name == name.Trim());
            if (exists)
            {
                TempData["ErrorMessage"] = "نوع التطريز هذا موجود مسبقاً.";
                return RedirectToAction(nameof(Categories));
            }

            var embroideryType = new EmbroideryType
            {
                Name = name.Trim(),
                Country = string.IsNullOrWhiteSpace(country) ? "فلسطين" : country.Trim()
            };

            _context.EmbroideryTypes.Add(embroideryType);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"تمت إضافة نوع التطريز \"{embroideryType.Name}\" بنجاح!";
            return RedirectToAction(nameof(Categories));
        }

        // POST: /Admin/UpdateEmbroideryType
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateEmbroideryType(int id, string name, string country)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["ErrorMessage"] = "يرجى إدخال اسم نوع التطريز.";
                return RedirectToAction(nameof(Categories));
            }

            var embroideryType = await _context.EmbroideryTypes.FirstOrDefaultAsync(e => e.EmbroideryTypeId == id);
            if (embroideryType == null)
            {
                TempData["ErrorMessage"] = "نوع التطريز غير موجود.";
                return RedirectToAction(nameof(Categories));
            }

            var duplicate = await _context.EmbroideryTypes.AnyAsync(e => e.Name == name.Trim() && e.EmbroideryTypeId != id);
            if (duplicate)
            {
                TempData["ErrorMessage"] = "نوع التطريز بهذا الاسم موجود مسبقاً.";
                return RedirectToAction(nameof(Categories));
            }

            embroideryType.Name = name.Trim();
            embroideryType.Country = string.IsNullOrWhiteSpace(country) ? "فلسطين" : country.Trim();
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"تم تعديل نوع التطريز \"{embroideryType.Name}\" بنجاح!";
            return RedirectToAction(nameof(Categories));
        }

        // POST: /Admin/DeleteEmbroideryType
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEmbroideryType(int id)
        {
            var embroideryType = await _context.EmbroideryTypes
                .Include(e => e.Products)
                .FirstOrDefaultAsync(e => e.EmbroideryTypeId == id);

            if (embroideryType == null)
            {
                TempData["ErrorMessage"] = "نوع التطريز غير موجود.";
                return RedirectToAction(nameof(Categories));
            }

            // فك ارتباط المنتجات بهذا النوع إن وجدت لمنع قيود الحذف
            foreach (var product in embroideryType.Products)
            {
                product.EmbroideryTypeId = null;
            }

            _context.EmbroideryTypes.Remove(embroideryType);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"تم حذف نوع التطريز \"{embroideryType.Name}\" بنجاح.";
            return RedirectToAction(nameof(Categories));
        }

        // GET: /Admin/Users (View Users - Read Only)
        public async Task<IActionResult> Users(string? search = null)
        {
            var usersQuery = _userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                usersQuery = usersQuery.Where(u =>
                    (u.UserName != null && u.UserName.Contains(search)) ||
                    (u.Email != null && u.Email.Contains(search)) ||
                    (u.PhoneNumber != null && u.PhoneNumber.Contains(search)));
            }

            var usersList = await usersQuery.OrderByDescending(u => u.CreatedAt).ToListAsync();

            var userIds = usersList.Select(u => u.Id).ToList();

            // حساب عدد المنتجات والطلبات لكل مستخدم بكفاءة
            var productsCountMap = await _context.Products
                .Where(p => userIds.Contains(p.UserId))
                .GroupBy(p => p.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);

            var ordersCountMap = await _context.Orders
                .Where(o => userIds.Contains(o.UserId))
                .GroupBy(o => o.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);

            var detailedUsers = new List<AdminUserDetailsViewModel>();

            foreach (var u in usersList)
            {
                var isUserAdmin = await _userManager.IsInRoleAsync(u, "Admin");
                detailedUsers.Add(new AdminUserDetailsViewModel
                {
                    Id = u.Id,
                    UserName = u.UserName ?? "بدون اسم",
                    Email = u.Email ?? "",
                    PhoneNumber = u.PhoneNumber,
                    Bio = u.Bio,
                    ProfileImage = u.ProfileImage,
                    Status = u.Status ?? "active",
                    CreatedAt = u.CreatedAt,
                    Role = isUserAdmin ? "مدير النظام (Admin)" : "مستخدم / بائع",
                    ProductsCount = productsCountMap.GetValueOrDefault(u.Id, 0),
                    OrdersCount = ordersCountMap.GetValueOrDefault(u.Id, 0)
                });
            }

            var vm = new AdminUsersViewModel
            {
                Users = detailedUsers,
                SearchTerm = search,
                TotalUsers = detailedUsers.Count
            };

            return View(vm);
        }

        // GET: /Admin/Orders (Processing Orders)
        public async Task<IActionResult> Orders(string status = "all", string? search = null)
        {
            var query = _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.ProductImages)
                .AsQueryable();

            var allOrders = await query.ToListAsync();

            int totalOrders = allOrders.Count;
            int pendingOrders = allOrders.Count(o => o.Status == "pending");
            int processingOrders = allOrders.Count(o => o.Status == "processing");
            int shippedOrders = allOrders.Count(o => o.Status == "shipped");
            int deliveredOrders = allOrders.Count(o => o.Status == "delivered" || o.Status == "completed");
            int cancelledOrders = allOrders.Count(o => o.Status == "cancelled");

            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                if (status == "delivered")
                {
                    query = query.Where(o => o.Status == "delivered" || o.Status == "completed");
                }
                else
                {
                    query = query.Where(o => o.Status == status);
                }
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(o =>
                    o.OrderId.ToString().Contains(search) ||
                    o.ShippingAddress.Contains(search) ||
                    (o.User != null && (o.User.UserName!.Contains(search) || o.User.Email!.Contains(search))));
            }

            var filteredOrders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();

            var vm = new AdminOrdersViewModel
            {
                Orders = filteredOrders,
                CurrentStatus = status,
                SearchTerm = search,
                TotalOrders = totalOrders,
                PendingOrders = pendingOrders,
                ProcessingOrders = processingOrders,
                ShippedOrders = shippedOrders,
                DeliveredOrders = deliveredOrders,
                CancelledOrders = cancelledOrders
            };

            return View(vm);
        }

        // POST: /Admin/UpdateOrderStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrderStatus(int id, string newStatus)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == id);
            if (order == null)
            {
                if (IsAjax()) return Json(new { success = false, message = "الطلب غير موجود." });
                TempData["ErrorMessage"] = "الطلب غير موجود.";
                return RedirectToAction(nameof(Orders));
            }

            order.Status = newStatus;
            await _context.SaveChangesAsync();

            string statusArabic = newStatus switch
            {
                "pending" => "قيد الانتظار",
                "processing" => "قيد التجهيز والمعالجة",
                "shipped" => "تم الشحن وهو في الطريق",
                "delivered" => "تم التسليم بنجاح",
                "cancelled" => "تم إلغاء الطلب",
                _ => newStatus
            };

            string message = $"تم تحديث حالة الطلب رقم #{order.OrderId} إلى: {statusArabic} ✓";

            if (IsAjax())
            {
                return Json(new { success = true, message, newStatus });
            }

            TempData["SuccessMessage"] = message;
            return RedirectToAction(nameof(Orders));
        }

        private bool IsAjax()
        {
            return Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                   Request.Headers.Accept.ToString().Contains("application/json");
        }
    }
}
