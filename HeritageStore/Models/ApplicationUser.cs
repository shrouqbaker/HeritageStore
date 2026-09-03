using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace HeritageStore.Models
{
    public class ApplicationUser : IdentityUser
    {
        [MaxLength(250)]
        public string? ProfileImage { get; set; }

        [MaxLength(500)]
        public string? Bio { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "active";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public ICollection<Product> Products { get; set; } = new List<Product>();
        public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
        public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}