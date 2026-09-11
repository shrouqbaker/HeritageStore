using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeritageStore.Models
{
    public class Product
    {
        [Key]
        public int ProductId { get; set; }

        [Required]
        public string UserId { get; set; }
        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; }

        [Required]
        public int CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public Category Category { get; set; }

        public int? EmbroideryTypeId { get; set; }
        [ForeignKey("EmbroideryTypeId")]
        public EmbroideryType EmbroideryType { get; set; }

        [Required]
        [MaxLength(50)]
        public string Country { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; }

        [Required]
        [MaxLength(1000)]
        public string Description { get; set; }

        public string? Story { get; set; }

        public string? HistoricalBackground { get; set; }

        [MaxLength(150)]
        public string? Occasion { get; set; }

        public string? AdditionalInfo { get; set; }

        [MaxLength(250)]
        public string? PickupAddress { get; set; }

        [Required]
        [MaxLength(20)]
        public string ListingType { get; set; } // for_sale / archive_only

        [Column(TypeName = "decimal(10,2)")]
        public decimal? Price { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "pending";

        [MaxLength(500)]
        public string? RejectionReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        // Navigation properties
        public ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();
        public ICollection<Embroidery> Embroideries { get; set; } = new List<Embroidery>();
        public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
        public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}