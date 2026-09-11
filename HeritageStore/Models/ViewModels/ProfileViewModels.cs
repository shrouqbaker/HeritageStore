using System.ComponentModel.DataAnnotations;
using HeritageStore.Models;

namespace HeritageStore.Models.ViewModels
{
    // نموذج تعديل الملف الشخصي للمستخدم المسجل
    public class ProfileEditViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Display(Name = "اسم المستخدم")]
        public string UserName { get; set; } = string.Empty;

        [Display(Name = "البريد الإلكتروني")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "رقم الهاتف")]
        [Phone(ErrorMessage = "رقم الهاتف غير صالح")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "نبذة تعريفية")]
        [MaxLength(500, ErrorMessage = "النبذة لا تتجاوز 500 حرف")]
        public string? Bio { get; set; }

        public string? ProfileImage { get; set; }

        [Display(Name = "تغيير الصورة الشخصية")]
        public IFormFile? NewProfileImage { get; set; }

        public DateTime CreatedAt { get; set; }

        // إحصائيات
        public int TotalProducts { get; set; }
        public int TotalOrders { get; set; }
        public int TotalFavorites { get; set; }

        // القوائم المعروضة داخل تبويبات الملف الشخصي
        public List<Product> Products { get; set; } = new();
        public List<Order> Orders { get; set; } = new();
    }

    // نموذج الملف الشخصي العام الذي يراه أي زائر
    public class PublicProfileViewModel
    {
        public ApplicationUser User { get; set; } = default!;
        public List<Product> Products { get; set; } = new();
        public int TotalProducts { get; set; }
        public int TotalForSale { get; set; }
        public int TotalArchived { get; set; }
    }

    // نموذج صفحة منتجاتي الخاصة بالمستخدم الحالي
    public class MyProductsViewModel
    {
        public List<Product> Products { get; set; } = new();
        public string CurrentStatus { get; set; } = "all";

        // إحصائيات الحالات
        public int TotalCount { get; set; }
        public int ApprovedCount { get; set; }
        public int PendingCount { get; set; }
        public int SoldCount { get; set; }
        public int RejectedCount { get; set; }
        public int ArchivedCount { get; set; }
    }
}
