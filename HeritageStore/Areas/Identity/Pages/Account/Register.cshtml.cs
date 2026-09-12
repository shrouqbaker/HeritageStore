// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using HeritageStore.Data;
using HeritageStore.Models;

namespace HeritageStore.Areas.Identity.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserStore<ApplicationUser> _userStore;
    private readonly IUserEmailStore<ApplicationUser> _emailStore;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RegisterModel> _logger;
    private readonly IEmailSender _emailSender;

    public RegisterModel(
        UserManager<ApplicationUser> userManager,
        IUserStore<ApplicationUser> userStore,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext context,
        ILogger<RegisterModel> logger,
        IEmailSender emailSender)
    {
        _userManager = userManager;
        _userStore = userStore;
        _emailStore = GetEmailStore();
        _signInManager = signInManager;
        _context = context;
        _logger = logger;
        _emailSender = emailSender;
    }

    [BindProperty]
    public string? GuestCartJson { get; set; }

    [BindProperty]
    public string? GuestFavsJson { get; set; }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    [BindProperty]
    public InputModel Input { get; set; } = default!;

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public string? ReturnUrl { get; set; }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public IList<AuthenticationScheme>? ExternalLogins { get; set; }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class InputModel
    {
        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = default!;

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = default!;

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string? ConfirmPassword { get; set; }

        [Required(ErrorMessage = "اسم المستخدم مطلوب")]
        [Display(Name = "اسم المستخدم")]
        public string Username { get; set; }

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [Phone(ErrorMessage = "صيغة رقم الهاتف غير صحيحة")]
        [Display(Name = "رقم الهاتف")]
        public string PhoneNumber { get; set; }
    }


    public async Task OnGetAsync(string? returnUrl = null)
    {
        returnUrl = returnUrl ?? Request.Query["ReturnUrl"].FirstOrDefault() ?? Request.Query["returnUrl"].FirstOrDefault() ?? Url.Content("~/");
        ReturnUrl = returnUrl;
        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl = returnUrl ?? Request.Form["returnUrl"].FirstOrDefault() ?? Request.Query["ReturnUrl"].FirstOrDefault() ?? Request.Query["returnUrl"].FirstOrDefault() ?? Url.Content("~/");
        ReturnUrl = returnUrl;

        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        if (ModelState.IsValid)
        {
            var user = CreateUser();

            var chosenUserName = !string.IsNullOrWhiteSpace(Input.Username) ? Input.Username.Trim() : Input.Email.Trim();

            // التحقق من اسم المستخدم
            var existingUser = await _userManager.FindByNameAsync(chosenUserName);
            if (existingUser != null)
            {
                ModelState.AddModelError("Input.Username", "اسم المستخدم هذا مسجل بالفعل، يرجى اختيار اسم آخر.");
                return Page();
            }

            user.PhoneNumber = Input.PhoneNumber;
            user.EmailConfirmed = true;

            await _userStore.SetUserNameAsync(user, chosenUserName, CancellationToken.None);
            await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
            var result = await _userManager.CreateAsync(user, Input.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation("User created a new account with password.");
                await _userManager.AddToRoleAsync(user, "User");

                await _signInManager.SignInAsync(user, isPersistent: false);

                // دمج عناصر السلة والمفضلة المخزنة في جهاز الزائر مباشرة
                await MergeGuestDataAsync(user.Id);

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return LocalRedirect(returnUrl);
                }
                return LocalRedirect("~/");
            }
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
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
                _logger.LogError(ex, "Error merging guest cart on register");
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
                _logger.LogError(ex, "Error merging guest favorites on register");
            }
        }
    }

    private ApplicationUser CreateUser()
    {
        return new ApplicationUser
        {
            Status = "active",
            CreatedAt = DateTime.Now
        };
    }
    private IUserEmailStore<ApplicationUser> GetEmailStore()
    {
        if (!_userManager.SupportsUserEmail)
        {
            throw new NotSupportedException("The default UI requires a user store with email support.");
        }
        return (IUserEmailStore<ApplicationUser>)_userStore;
    }
}
