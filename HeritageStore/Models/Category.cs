using System.ComponentModel.DataAnnotations;
using Microsoft.CodeAnalysis;

namespace HeritageStore.Models
{
    public class Category
    {
        [Key]
        public int CategoryId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [MaxLength(250)]
        public string? ImageUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        // Navigation property
        public ICollection<Product> Products { get; set; }
    }
}