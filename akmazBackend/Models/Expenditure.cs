// Models/Expenditure.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AkmazBackend.Models
{
    [Table("tblExpenditures")]
    public class Expenditure
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [MaxLength(255)]
        public string? Description { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation property
        public ICollection<AuditorAcknowledgment> Acknowledgments { get; set; } = new List<AuditorAcknowledgment>();
    }
}