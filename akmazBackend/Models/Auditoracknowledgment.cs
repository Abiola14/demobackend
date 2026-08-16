// Models/AuditorAcknowledgment.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AkmazBackend.Models
{
    [Table("AuditorAcknowledgments")]
    public class AuditorAcknowledgment
    {
        [Key]
        public int Id { get; set; }

        // Null = batch acknowledgment (covers a date range, not a single record)
        public int? ExpenditureId { get; set; }

        [ForeignKey("ExpenditureId")]
        public Expenditure? Expenditure { get; set; }

        [Required]
        [MaxLength(100)]
        public string AuditorName { get; set; } = string.Empty;

        [MaxLength(150)]
        public string AuditorEmail { get; set; } = string.Empty;

        // "Approved" | "Flagged" | "Reviewed"
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Reviewed";

        [MaxLength(500)]
        public string? Notes { get; set; }

        public DateTime AcknowledgedAt { get; set; } = DateTime.Now;

        // Used for batch acknowledgments only
        public DateTime? PeriodFrom { get; set; }
        public DateTime? PeriodTo   { get; set; }
    }
}