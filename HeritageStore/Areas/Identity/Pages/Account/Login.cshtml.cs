// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HeritageStore.Data;
using HeritageStore.Models;
namespace HeritageStore.Areas.Identity.Pages.Account;

public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        ILogger<LoginModel> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _context = context;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = default!;

    [BindProperty]
    public string? GuestCartJson { get; set; }

    [BindProperty]
    public string? GuestFavsJson { get; set; }

    public IList<AuthenticationScheme>? ExternalLogins { get; set; }

    public string? ReturnUrl { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "يرجى إدخال اسم المستخدم أو البريد الإلكتروني")]
        [Display(Name = "اسم المستخدم أو البريد الإلكتروني")]
        public string Email { get; set; } = default!;

        [Required(ErrorMessage = "يرجى إدخال كلمة المرور")]
        [DataType(DataType.Password)]
        [Display(Name = "كلمة المرور")]
        public string Password { get; set; } = default!;

        [Display(Name = "تذكرني")]
        public bool RememberMe { get; set; }
    }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        returnUrl = returnUrl ?? Request.Query["ReturnUrl"].FirstOrDefault() ?? Request.Query["returnUrl"].FirstOrDefault() ?? Url.Content("~/");

        // Clear the existing external cookie to ensure a clean login process
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl = returnUrl ?? Request.Form["returnUrl"].FirstOrDefault() ?? Request.Query["ReturnUrl"].FirstOrDefault() ?? Request.Query["returnUrl"].FirstOrDefault() ?? Url.Content("~/");
        ReturnUrl = returnUrl;

        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

        if (ModelState.IsValid)
        {
            var loginIdentifier = Input.Email?.Trim();
            if (string.IsNullOrEmpty(loginIdentifier))
            {
                ModelState.AddModelError(string.Empty, "يرجى إدخال اسم المستخدم أو البريد الإلكتروني.");
                return Page();
            }

            // البحث عن الحساب بواسطة البريد الإلكتروني أولاً ثم بواسطة اسم المستخدم
            var user = await _userManager.FindByEmailAsync(loginIdentifier)
                       ?? await _userManager.FindByNameAsync(loginIdentifier);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "بيانات الدخول غير صحيحة. لم يتم العثور على حساب بهذا البريد أو اسم المستخدم.");
                return Page();
            }

            // التحقق من حالة الحساب
            if (!string.IsNullOrEmpty(user.Status) && user.Status != "active")
            {
                ModelState.AddModelError(string.Empty, "هذا الحساب غير نشط حالياً أو تم إيقافه.");
                return Page();
            }

            // التأكد من تفعيل تأكيد البريد تلقائياً لضمان عدم حظر تسجيل الدخول
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
            }

            // محاولة تسجيل الدخول باستخدام اسم المستخدم الفعلي
            var result = await _signInManager.PasswordSignInAsync(
                user.UserName!,
                Input.Password,
                Input.RememberMe,
                lockoutOnFailure: false);

            if (result.Succeeded)
            {
                _logger.LogInformation("User logged in.");

                // دمج عناصر السلة والمفضلة المخزنة في جهاز الزائر مباشرة قبل التحويل
                await MergeGuestDataAsync(user.Id);

                if (await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    if (!string.IsNullOrEmpty(returnUrl) && returnUrl != "/" && returnUrl != Url.Content("~/") && Url.IsLocalUrl(returnUrl))
                    {
                        return LocalRedirect(returnUrl);
                    }
                    return LocalRedirect("/Admin");
                }

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return LocalRedirect(returnUrl);
                }
                return LocalRedirect("~/");
            }
            if (result.RequiresTwoFactor)
            {
                return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
            }
            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account locked out.");
                return RedirectToPage("./Lockout");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "كلمة المرور غير صحيحة. يرجى التأكد وإعادة المحاولة.");
                return Page();
            }
        }

        // If we got this far, something failed, redisplay form
        return Page();
    }

    private async Task MergeGuestDataAsync(string userId)
    {
        // دمج عناصر السلة
        if (!string.IsNullOrWhiteSpace(GuestCartJson))
        {
            try
            {
                var productIds = System.Text.Json.JsonSerializer.Deserialize<List<int>>(GuestCartJson);
                if (productIds != null && productIds.Any())
                {
                    var existingIds = await _context.CartItems
                        .Where(c => c.UserId == userId)
                        .Select(c => c.ProductId)
                        .ToListAsync();

                    var newIds = productIds.Distinct().Where(id => !existingIds.Contains(id)).ToList();
                    if (newIds.Any())
                    {
                        var validProducts = await _context.Products
                            .Where(p => newIds.Contains(p.ProductId) && p.ListingType == "for_sale" && p.Status == "approved")
                            .Select(p => p.ProductId)
                            .ToListAsync();

                        foreach (var pid in validProducts)
                        {
                            _context.CartItems.Add(new CartItem
                            {
                                UserId = userId,
                                ProductId = pid,
                                AddedAt = DateTime.Now
                            });
                        }
                        await _context.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error merging guest cart on login");
            }
        }

        // دمج عناصر المفضلة
        if (!string.IsNullOrWhiteSpace(GuestFavsJson))
        {
            try
            {
                var favIds = System.Text.Json.JsonSerializer.Deserialize<List<int>>(GuestFavsJson);
                if (favIds != null && favIds.Any())
                {
                    var existingFavs = await _context.Favorites
                        .Where(f => f.UserId == userId)
                        .Select(f => f.ProductId)
                        .ToListAsync();

                    var newFavIds = favIds.Distinct().Where(id => !existingFavs.Contains(id)).ToList();
                    if (newFavIds.Any())
                    {
                        var validFavProducts = await _context.Products
                            .Where(p => newFavIds.Contains(p.ProductId))
                            .Select(p => p.ProductId)
                            .ToListAsync();

                        foreach (var pid in validFavProducts)
                        {
                            _context.Favorites.Add(new Favorite
                            {
                                UserId = userId,
                                ProductId = pid,
                                CreatedAt = DateTime.Now
                            });
                        }
                        await _context.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error merging guest favorites on login");
            }
        }
    }
}
