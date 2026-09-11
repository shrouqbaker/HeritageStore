using System;
using System.Collections.Generic;
using HeritageStore.Models;

namespace HeritageStore.Models.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int TotalProducts { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalSales { get; set; }
        public int TotalUsers { get; set; }
        public int PendingProductsCount { get; set; }
        public int ApprovedProductsCount { get; set; }
        public int SoldCount { get; set; }
        public int RejectedProductsCount { get; set; }
        public int ArchivedProductsCount { get; set; }

        public List<Order> RecentOrders { get; set; } = new();
        public List<Product> PendingProducts { get; set; } = new();
        public List<TopCategoryStat> TopCategories { get; set; } = new();
        public List<MonthlySalesTrend> MonthlyTrend { get; set; } = new();
    }

    public class TopCategoryStat
    {
        public string CategoryName { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TotalSales { get; set; }
        public int Percentage { get; set; }
    }

    public class MonthlySalesTrend
    {
        public string MonthName { get; set; } = string.Empty;
        public decimal Sales { get; set; }
        public int OrdersCount { get; set; }
    }

    public class AdminProductsViewModel
    {
        public List<Product> Products { get; set; } = new();
        public string CurrentStatus { get; set; } = "all";
        public string? SearchTerm { get; set; }
        public int TotalCount { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }
        public int SoldCount { get; set; }
        public int PendingCount { get; set; }
    }

    public class AdminCategoriesViewModel
    {
        public List<CategoryWithCount> Categories { get; set; } = new();
        public List<EmbroideryTypeWithCount> EmbroideryTypes { get; set; } = new();
        public int TotalCategories { get; set; }
        public int TotalEmbroideryTypes { get; set; }
    }

    public class CategoryWithCount
    {
        public Category Category { get; set; } = null!;
        public int ProductsCount { get; set; }
    }

    public class EmbroideryTypeWithCount
    {
        public EmbroideryType EmbroideryType { get; set; } = null!;
        public int ProductsCount { get; set; }
    }

    public class AdminUsersViewModel
    {
        public List<AdminUserDetailsViewModel> Users { get; set; } = new();
        public string? SearchTerm { get; set; }
        public int TotalUsers { get; set; }
    }

    public class AdminUserDetailsViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Bio { get; set; }
        public string? ProfileImage { get; set; }
        public string Status { get; set; } = "active";
        public DateTime CreatedAt { get; set; }
        public string Role { get; set; } = "User";
        public int ProductsCount { get; set; }
        public int OrdersCount { get; set; }
    }

    public class AdminOrdersViewModel
    {
        public List<Order> Orders { get; set; } = new();
        public string CurrentStatus { get; set; } = "all";
        public string? SearchTerm { get; set; }
        public int TotalOrders { get; set; }
        public int PendingOrders { get; set; }
        public int ProcessingOrders { get; set; }
        public int ShippedOrders { get; set; }
        public int DeliveredOrders { get; set; }
        public int CancelledOrders { get; set; }
    }
}
