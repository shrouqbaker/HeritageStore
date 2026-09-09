namespace HeritageStore.Models.ViewModels
{
    public class ExploreViewModel
    {
        // نتائج
        public List<Product> Products { get; set; } = new();

        // بيانات لتعبئة الفلاتر
        public List<Category> Categories { get; set; } = new();
        public List<EmbroideryType> EmbroideryTypes { get; set; } = new();

        // الفلاتر المختارة حاليًا (من الـQuery String)
        public int? CategoryId { get; set; }
        public string? Country { get; set; }
        public List<int> EmbroideryTypeIds { get; set; } = new();
        public string? ListingType { get; set; } // for_sale / archive_only / null(الكل)
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string? SearchTerm { get; set; }
        public decimal MaxPriceLimit { get; set; } = 1000;

        // ترقيم الصفحات
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalCount { get; set; }
    }
}