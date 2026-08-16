// Controllers/ExpendituresController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AkmazBackend.Data;
using AkmazBackend.Models;

namespace AkmazBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExpendituresController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ExpendituresController(AppDbContext context)
        {
            _context = context;
        }

        // ── GET all expenditures (newest first) ───────────────────────
        [HttpGet]
        public async Task<IActionResult> GetExpenditures()
        {
            var expenditures = await _context.tblExpenditures
                .Include(e => e.Acknowledgments)
                .OrderByDescending(e => e.Date)
                .ToListAsync();

            var result = expenditures.Select(e => {
                var ack = e.Acknowledgments.FirstOrDefault();
                return new {
                    e.Id,
                    e.Type,
                    e.Amount,
                    e.Description,
                    e.Date,
                    e.CreatedBy,
                    e.CreatedAt,
                    isAcknowledged       = ack != null,
                    acknowledgedBy       = ack?.AuditorName,
                    acknowledgedAt       = ack?.AcknowledgedAt,
                    acknowledgmentStatus = ack?.Status,
                };
            });

            return Ok(result);
        }

        // ── GET a single expenditure by ID ────────────────────────────
        [HttpGet("{id}")]
        public async Task<IActionResult> GetExpenditure(int id)
        {
            var expenditure = await _context.tblExpenditures.FindAsync(id);
            if (expenditure == null)
                return NotFound("Expenditure not found.");
            return Ok(expenditure);
        }

        // ── ADD a new expenditure (Admin) ─────────────────────────────
        [HttpPost("add")]
        public async Task<IActionResult> AddExpenditure([FromBody] Expenditure expenditure)
        {
            if (expenditure == null)
                return BadRequest("Expenditure data is required.");
            if (string.IsNullOrWhiteSpace(expenditure.Type))
                return BadRequest("Type is required.");
            if (expenditure.Amount <= 0)
                return BadRequest("Amount must be greater than zero.");
            if (expenditure.Date == default)
                expenditure.Date = DateTime.Now;

            expenditure.CreatedAt = DateTime.Now;

            try
            {
                _context.tblExpenditures.Add(expenditure);
                await _context.SaveChangesAsync();
                return Ok(expenditure);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Database error: {ex.Message}");
            }
        }

        // ── EDIT a Not-Approved expenditure (Admin only) ──────────────
        // Only allowed if acknowledgment status is "Flagged" (Not Approved)
        // Editing resets the acknowledgment so auditor reviews again
        [HttpPut("edit/{id}")]
        public async Task<IActionResult> EditExpenditure(int id, [FromBody] Expenditure updated)
        {
            if (updated == null)
                return BadRequest("Expenditure data is required.");

            var expenditure = await _context.tblExpenditures
                .Include(e => e.Acknowledgments)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (expenditure == null)
                return NotFound("Expenditure not found.");

            var ack = expenditure.Acknowledgments.FirstOrDefault();

            // Block editing if Approved or has no acknowledgment issue
            if (ack == null)
                return BadRequest("Only flagged (Not Approved) expenditures can be edited.");
            if (ack.Status != "Flagged")
                return BadRequest("Only flagged (Not Approved) expenditures can be edited.");

            if (string.IsNullOrWhiteSpace(updated.Type))
                return BadRequest("Type is required.");
            if (updated.Amount <= 0)
                return BadRequest("Amount must be greater than zero.");

            expenditure.Type        = updated.Type.Trim();
            expenditure.Amount      = updated.Amount;
            expenditure.Description = updated.Description?.Trim();
            expenditure.Date        = updated.Date == default ? expenditure.Date : updated.Date;

            // Remove old acknowledgment → resets to Pending for auditor to re-review
            _context.tblAcknowledgments.Remove(ack);

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "Expenditure updated and reset for auditor review." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Update failed: {ex.Message}");
            }
        }

        // ── ACKNOWLEDGE an expenditure (Auditor) ──────────────────────
        [HttpPut("acknowledge/{id}")]
        public async Task<IActionResult> AcknowledgeExpenditure(int id, [FromBody] AcknowledgeExpenditureDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.AuditorName))
                return BadRequest("AuditorName is required.");

            var expenditure = await _context.tblExpenditures
                .Include(e => e.Acknowledgments)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (expenditure == null)
                return NotFound("Expenditure not found.");

            if (expenditure.Acknowledgments.Any())
                return BadRequest("This expenditure has already been acknowledged.");

            var validStatuses = new[] { "Approved", "Flagged" };
            if (!validStatuses.Contains(dto.Status))
                return BadRequest("Status must be 'Approved' or 'Flagged'.");

            var ack = new AuditorAcknowledgment
            {
                ExpenditureId  = id,
                AuditorName    = dto.AuditorName.Trim(),
                AuditorEmail   = dto.AuditorEmail?.Trim() ?? string.Empty,
                Status         = dto.Status,
                Notes          = dto.Notes?.Trim(),
                AcknowledgedAt = DateTime.Now
            };

            try
            {
                _context.tblAcknowledgments.Add(ack);
                await _context.SaveChangesAsync();
                return Ok(new {
                    ack.Id,
                    ack.ExpenditureId,
                    ack.AuditorName,
                    ack.Status,
                    ack.Notes,
                    ack.AcknowledgedAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Acknowledge failed: {ex.Message}");
            }
        }

        // ── GET all acknowledgment records (audit trail) ──────────────
        [HttpGet("acknowledgments")]
        public async Task<IActionResult> GetAcknowledgments()
        {
            var acks = await _context.tblAcknowledgments
                .OrderByDescending(a => a.AcknowledgedAt)
                .ToListAsync();

            var result = acks.Select(a => new {
                a.Id,
                a.ExpenditureId,
                a.AuditorName,
                a.AuditorEmail,
                a.Status,
                a.Notes,
                a.AcknowledgedAt,
                a.PeriodFrom,
                a.PeriodTo
            });

            return Ok(result);
        }
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    public class AcknowledgeExpenditureDto
    {
        public string  AuditorName  { get; set; } = string.Empty;
        public string? AuditorEmail { get; set; }
        public string  Status       { get; set; } = "Approved"; // Approved | Flagged
        public string? Notes        { get; set; }
    }

    public class AcknowledgeBatchDto
    {
        public string   AuditorName  { get; set; } = string.Empty;
        public string?  AuditorEmail { get; set; }
        public string   Status       { get; set; } = "Approved";
        public string?  Notes        { get; set; }
        public DateTime PeriodFrom   { get; set; }
        public DateTime PeriodTo     { get; set; }
    }
}