using System.ComponentModel.DataAnnotations;
using Microsoft.CodeAnalysis;

namespace HeritageStore.Models
{
    public class EmbroideryType
    {
        [Key]
        public int EmbroideryTypeId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; }

        [Required]
        [MaxLength(50)]
        public string Country { get; set; }

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}