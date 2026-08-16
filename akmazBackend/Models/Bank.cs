// Models/BankDeposit.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AkmazBackend.Models
{
    [Table("tblBankDeposits")]
    public class BankDeposit
    {
        [Key]
        public int Id { get; set; }

        public decimal Amount { get; set; }

        public string? Description { get; set; }  // ← nullable

        public DateTime Date { get; set; }

        public string? CreatedBy { get; set; }     // ← nullable

        public bool IsConfirmed { get; set; } = false;

        public string? ConfirmedBy { get; set; }   // ← already nullable ✅

        public DateTime? ConfirmedAt { get; set; } // ← already nullable ✅
    }
}