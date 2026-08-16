using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;

namespace AkmazBackend.Models
{
    [Table("tblSales")]
    public class Sale
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(150)]
        public string Product { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string CustomerName { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string Quantity { get; set; } = string.Empty;

        [Required]
        public decimal UnitPrice { get; set; }

        [Required]
        public decimal TotalPrice { get; set; }

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime SoldAt { get; set; }

        public DateTime? ModifiedAt { get; set; }

        [StringLength(100)]
        public string? ModifiedBy { get; set; }

        [Required, StringLength(20)]
        public string PaymentStatus { get; set; } = "Unpaid";

        [StringLength(100)]
        public string? ConfirmedBy { get; set; }

        public DateTime? ConfirmedAt { get; set; }

        // ✅ EXISTING METHOD
        public static int ExtractQuantity(string quantity)
        {
            if (string.IsNullOrWhiteSpace(quantity)) return 0;
            var match = Regex.Match(quantity, @"\d+");
            return int.TryParse(match.Value, out int n) ? n : 0;
        }

        // ✅ ADD THIS METHOD (FIX)
        public void RecalculateTotal()
        {
            int qty = ExtractQuantity(this.Quantity);
            this.TotalPrice = qty * this.UnitPrice;
        }
    }
}