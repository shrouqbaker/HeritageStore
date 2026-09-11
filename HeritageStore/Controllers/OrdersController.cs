using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeritageStore.Data;
using HeritageStore.Models;

namespace HeritageStore.Controllers
{
    [Authorize]
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        // TODO: عدّلي قيمة التوصيل الثابتة هاي لو عندك منطق مختلف (حسب المنطقة مثلاً)
        private const decimal FlatDeliveryCost = 3.0m;

        public OrdersController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Orders أو /Order
        [HttpGet]
        [Route("Orders")]
        [Route("Orders/Index")]
        [Route("Order")]
        [Route("Order/Index")]
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.ProductImages)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        // GET: /Orders/Checkout
        [Route("Orders/Checkout")]
        [Route("Order/Checkout")]
        public async Task<IActionResult> Checkout()
        {
            var userId = _userManager.GetUserId(User);

            var cartItems = await _context.CartItems
                .Include(c => c.Product)
                    .ThenInclude(p => p.ProductImages)
                .Where(c => c.UserId == userId)
                .ToListAsync();

            var availableItems = cartItems
                .Where(c => IsAvailable(c.Product))
                .ToList();

            if (!availableItems.Any())
            {
                TempData["Error"] = "لا توجد قطع متاحة للشراء حاليًا.";
                return RedirectToAction("Index", "Cart");
            }

            var subtotal = availableItems.Sum(c => c.Product!.Price ?? 0m);

            var vm = new CheckoutViewModel
            {
                Items = availableItems,
                Subtotal = subtotal,
                DeliveryCost = FlatDeliveryCost,
                Total = subtotal + FlatDeliveryCost
            };

            return View(vm);
        }

        // POST: /Orders/Checkout
        [HttpPost]
        [Route("Orders/Checkout")]
        [Route("Order/Checkout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(CheckoutViewModel form)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(form.ShippingAddress))
            {
                ModelState.AddModelError(nameof(form.ShippingAddress), "عنوان التوصيل مطلوب");
            }

            if (form.PaymentMethod != "cash" && form.PaymentMethod != "visa")
            {
                ModelState.AddModelError(nameof(form.PaymentMethod), "اختاري طريقة دفع صحيحة");
            }

            // تحقق شكلي من بيانات الفيزا — بس لو اختارت الدفع بالفيزا
            if (form.PaymentMethod == "visa")
            {
                var cardDigits = (form.CardNumber ?? "").Replace(" ", "");

                if (string.IsNullOrWhiteSpace(form.CardHolderName))
                {
                    ModelState.AddModelError(nameof(form.CardHolderName), "اسم صاحب البطاقة مطلوب");
                }

                if (cardDigits.Length != 16 || !cardDigits.All(char.IsDigit))
                {
                    ModelState.AddModelError(nameof(form.CardNumber), "رقم البطاقة لازم يكون 16 رقم");
                }

                if (string.IsNullOrWhiteSpace(form.CardExpiry) ||
                    !System.Text.RegularExpressions.Regex.IsMatch(form.CardExpiry, @"^(0[1-9]|1[0-2])\/\d{2}$"))
                {
                    ModelState.AddModelError(nameof(form.CardExpiry), "صيغة تاريخ الانتهاء لازم تكون MM/YY");
                }
                else
                {
                    // تحقق إنه التاريخ لسا ما انتهى
                    var parts = form.CardExpiry.Split('/');
                    int expMonth = int.Parse(parts[0]);
                    int expYear = 2000 + int.Parse(parts[1]);
                    var expiryDate = new DateTime(expYear, expMonth, 1).AddMonths(1).AddDays(-1);
                    if (expiryDate < DateTime.Today)
                    {
                        ModelState.AddModelError(nameof(form.CardExpiry), "البطاقة منتهية الصلاحية");
                    }
                }

                if (string.IsNullOrWhiteSpace(form.CardCvv) || form.CardCvv.Length < 3 || !form.CardCvv.All(char.IsDigit))
                {
                    ModelState.AddModelError(nameof(form.CardCvv), "رمز الأمان يجب أن يتكون من 3 أو 4 أرقام.");
                }
            }

            var cartItems = await _context.CartItems
                .Include(c => c.Product)
                    .ThenInclude(p => p.ProductImages)
                .Where(c => c.UserId == userId)
                .ToListAsync();

            // إعادة التحقق من التوفر من قاعدة البيانات مباشرة (مش من الفورم)
            // عشان نمنع حالة إنه القطعة تنباع من حدا تاني بين ما المستخدم فتح صفحة السلة وضغط "إتمام الطلب"
            var availableItems = cartItems.Where(c => IsAvailable(c.Product)).ToList();
            var unavailableItems = cartItems.Where(c => !IsAvailable(c.Product)).ToList();

            if (!availableItems.Any())
            {
                TempData["Error"] = "القطع الموجودة في سلتك لم تعد متاحة. اكتشفي قطعًا أخرى مميزة من حِكاية.";
                return RedirectToAction("Index", "Cart");
            }

            if (unavailableItems.Any())
            {
                // بنشيلهم من السلة تلقائيًا وبنطلب من المستخدم يراجع طلبه
                _context.CartItems.RemoveRange(unavailableItems);
                await _context.SaveChangesAsync();

                TempData["Error"] = $"{unavailableItems.Count} قطعة لم تعد متاحة وتم حذفها من سلتك. راجعي السلة وأكملي طلبك.\r\n";
                return RedirectToAction("Index", "Cart");
            }

            if (!ModelState.IsValid)
            {
                var subtotal = availableItems.Sum(c => c.Product!.Price ?? 0m);
                form.Items = availableItems;
                form.Subtotal = subtotal;
                form.DeliveryCost = FlatDeliveryCost;
                form.Total = subtotal + FlatDeliveryCost;
                return View(form);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var orderSubtotal = availableItems.Sum(c => c.Product!.Price ?? 0m);

                var order = new Order
                {
                    UserId = userId!,
                    ShippingAddress = form.ShippingAddress,
                    PaymentMethod = form.PaymentMethod,
                    Status = "pending",
                    DeliveryCost = FlatDeliveryCost,
                    TotalPrice = orderSubtotal + FlatDeliveryCost
                };
                _context.Orders.Add(order);
                await _context.SaveChangesAsync(); // عشان ناخد OrderId

                foreach (var item in availableItems)
                {
                    _context.OrderItems.Add(new OrderItem
                    {
                        OrderId = order.OrderId,
                        ProductId = item.ProductId,
                        PriceAtPurchase = (decimal)item.Product!.Price!
                    });

                    // القطعة تصير مباعة فورًا، أي حدا تاني عندها بسلته رح تظهر عنده "مباعة"
                    item.Product!.Status = "sold";
                }

                _context.CartItems.RemoveRange(availableItems);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return RedirectToAction(nameof(Confirmation), new { id = order.OrderId });
            }
            catch
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "حدث خطأ أثناء إتمام الطلب. يرجى المحاولة مرة أخرى.";
                return RedirectToAction("Index", "Cart");
            }
        }

        // GET: /Orders/Confirmation/5
        [HttpGet]
        [Route("Orders/Confirmation/{id:int}")]
        [Route("Order/Confirmation/{id:int}")]
        public async Task<IActionResult> Confirmation(int id)
        {
            var userId = _userManager.GetUserId(User);

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p!.ProductImages)
                .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        private static bool IsAvailable(Product? p) =>
            p != null && p.ListingType == "for_sale" && p.Status == "approved";
    }

    // TODO: انقليها لملف منفصل بمجلد Models/ViewModels لو بدك تنظيم أوضح
    public class CheckoutViewModel
    {
        public List<CartItem> Items { get; set; } = new();
        public decimal Subtotal { get; set; }
        public decimal DeliveryCost { get; set; }
        public decimal Total { get; set; }

        // TODO: لو عندك حقول تانية بالعنوان (مدينة/رقم هاتف) ضيفيها هون
        public string ShippingAddress { get; set; } = string.Empty;

        // "cash" أو "visa"
        public string PaymentMethod { get; set; } = "cash";

        // حقول بطاقة الفيزا — بتنستخدم للتحقق الشكلي بس، وما بتنخزن أبدًا بقاعدة البيانات
        public string? CardHolderName { get; set; }
        public string? CardNumber { get; set; }
        public string? CardExpiry { get; set; }
        public string? CardCvv { get; set; }
    }
}