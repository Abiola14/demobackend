// Models/FishInventory.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AkmazBackend.Models
{
    [Table("tblInventory")]
    public class FishInventory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Column("Name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("Quantity")]
        public string Quantity { get; set; } = string.Empty;

        [Required]
        [Column("Price")]
        public decimal Price { get; set; }

        [Column("TotalPrice")]  // ← MATCHES YOUR DB
        public decimal TotalPrice { get; set; }  // ← PROPERTY NAME

        [Column("Supplier")]
        public string? Supplier { get; set; }

        [Column("Created_At")]
        public DateTime Created_At { get; set; } = DateTime.Now;

        [Column("Created_By")]
        public string? Created_By { get; set; }

        [Column("Modified_At")]
        public DateTime? Modified_At { get; set; }

        [Column("Modified_By")]
        public string? Modified_By { get; set; }

        public static int ExtractQuantity(string quantity)
        {
            if (string.IsNullOrEmpty(quantity)) return 0;
            var match = System.Text.RegularExpressions.Regex.Match(quantity, @"\d+");
            return int.TryParse(match.Value, out int n) ? n : 0;
        }
    }
}