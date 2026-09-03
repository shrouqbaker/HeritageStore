using System.ComponentModel.DataAnnotations;

namespace HeritageStore.Models.ViewModels
{
    public class ProductFormViewModel
    {
        [Required(ErrorMessage = "التصنيف مطلوب")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "الدولة مطلوبة")]
        public string Country { get; set; }

        public int? EmbroideryTypeId { get; set; }

        [Required(ErrorMessage = "اسم الثوب مطلوب")]
        [MaxLength(150)]
        public string Name { get; set; }

        [Required(ErrorMessage = "الوصف مطلوب")]
        [MaxLength(1000)]
        public string Description { get; set; }

        public string? Story { get; set; }
        public string? HistoricalBackground { get; set; }

        [MaxLength(150)]
        public string? Occasion { get; set; }

        public string? AdditionalInfo { get; set; }

        [Required(ErrorMessage = "عنوان الاستلام مطلوب")]
        [MaxLength(250)]
        public string PickupAddress { get; set; }

        [Required]
        public string ListingType { get; set; } = "archive_only"; // for_sale / archive_only

        [Range(1, 1000000, ErrorMessage = "السعر غير صحيح")]
        public decimal? Price { get; set; }

        // الصور
        public List<IFormFile> Images { get; set; } = new();
        public int MainImageIndex { get; set; } = 0;

        // التطريزات (حتى 3)
        public List<EmbroideryInputModel> Embroideries { get; set; } = new();

        // لتعبئة القوائم المنسدلة بالـView
        public List<Category>? CategoryList { get; set; }
        public List<EmbroideryType>? EmbroideryTypeList { get; set; }
    }

    public class EmbroideryInputModel
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public IFormFile? Image { get; set; }
    }
}