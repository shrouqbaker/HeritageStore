namespace HeritageStore.Models.ViewModels
{
    public class HomeViewModel
    {
        public List<Category> Categories { get; set; } = new();
        public List<Product> LatestProducts { get; set; } = new();
        public HashSet<int> FavoritedProductIds { get; set; } = new();
        public int ForSaleProducts { get; set; }
        public int ArchivedProducts { get; set; }
        public int TotalCategories { get; set; }
        public int TotalUsers { get; set; }
    }
}