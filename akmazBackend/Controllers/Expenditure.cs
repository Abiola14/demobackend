// Controllers/ExpendituresController.cs

using Microsoft.AspNetCore.Authorization;
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

        // ─────────────────────────────────────────────────────────────
        // GET ALL EXPENDITURES
        // Newest first
        // ─────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetExpenditures()
        {
            var expenditures = await _context.tblExpenditures
                .Include(e => e.Acknowledgments)
                .OrderByDescending(e => e.Date)
                .ToListAsync();

            var result = expenditures.Select(e =>
            {
                var ack = e.Acknowledgments.FirstOrDefault();

                return new
                {
                    e.Id,
                    e.Type,
                    e.Amount,
                    e.Description,
                    e.Date,
                    e.CreatedBy,
                    e.CreatedAt,

                    // Only Approved or Rejected counts as a completed review.
                    // Pending means the Admin has resubmitted it and Auditor
                    // needs to review it again.
                    isAcknowledged =
                        ack != null &&
                        (ack.Status == "Approved" || ack.Status == "Rejected"),

                    acknowledgedBy = ack?.AuditorName,
                    acknowledgedAt = ack?.AcknowledgedAt,
                    acknowledgmentStatus = ack?.Status,

                    // This contains the rejection reason when rejected.
                    acknowledgmentNotes = ack?.Notes
                };
            });

            return Ok(result);
        }

        // ─────────────────────────────────────────────────────────────
        // GET SINGLE EXPENDITURE
        // ─────────────────────────────────────────────────────────────
        [HttpGet("{id}")]
        public async Task<IActionResult> GetExpenditure(int id)
        {
            var expenditure = await _context.tblExpenditures
                .Include(e => e.Acknowledgments)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (expenditure == null)
                return NotFound("Expenditure not found.");

            return Ok(expenditure);
        }

        // ─────────────────────────────────────────────────────────────
        // ADD NEW EXPENDITURE
        // ADMIN ONLY
        // ─────────────────────────────────────────────────────────────
        [HttpPost("add")]
        public async Task<IActionResult> AddExpenditure(
            [FromBody] Expenditure expenditure)
        {
            if (expenditure == null)
                return BadRequest("Expenditure data is required.");

            if (string.IsNullOrWhiteSpace(expenditure.Type))
                return BadRequest("Type is required.");

            if (expenditure.Amount <= 0)
                return BadRequest("Amount must be greater than zero.");

            if (expenditure.Date == default)
                expenditure.Date = DateTime.Now;

            expenditure.Type = expenditure.Type.Trim();

            if (!string.IsNullOrWhiteSpace(expenditure.Description))
                expenditure.Description = expenditure.Description.Trim();

            expenditure.CreatedAt = DateTime.Now;

            try
            {
                _context.tblExpenditures.Add(expenditure);

                await _context.SaveChangesAsync();

                return Ok(expenditure);
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    $"Database error: {ex.Message}"
                );
            }
        }

        // ─────────────────────────────────────────────────────────────
        // EDIT + RESUBMIT REJECTED EXPENDITURE
        // ADMIN ONLY
        //
        // When Admin edits a rejected expenditure:
        //
        // Rejected
        //    ↓
        // Admin edits
        //    ↓
        // Pending
        //    ↓
        // Auditor reviews again
        //
        // IMPORTANT:
        // We do NOT delete the acknowledgment record.
        // The previous rejection note remains available until the
        // Auditor makes the next decision.
        // ─────────────────────────────────────────────────────────────
        [HttpPut("edit/{id}")]
        public async Task<IActionResult> EditExpenditure(
            int id,
            [FromBody] Expenditure updated)
        {
            if (updated == null)
                return BadRequest("Expenditure data is required.");

            var expenditure = await _context.tblExpenditures
                .Include(e => e.Acknowledgments)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (expenditure == null)
                return NotFound("Expenditure not found.");

            var ack = expenditure.Acknowledgments.FirstOrDefault();

            // An expenditure must have a rejection before it can be edited.
            if (ack == null)
            {
                return BadRequest(
                    "Only rejected expenditures can be edited."
                );
            }

            if (ack.Status != "Rejected")
            {
                return BadRequest(
                    "Only rejected expenditures can be edited."
                );
            }

            if (string.IsNullOrWhiteSpace(updated.Type))
                return BadRequest("Type is required.");

            if (updated.Amount <= 0)
                return BadRequest(
                    "Amount must be greater than zero."
                );

            if (updated.Date == default)
                return BadRequest("Date is required.");

            expenditure.Type = updated.Type.Trim();

            expenditure.Amount = updated.Amount;

            expenditure.Description =
                string.IsNullOrWhiteSpace(updated.Description)
                    ? null
                    : updated.Description.Trim();

            expenditure.Date = updated.Date;

            // ---------------------------------------------------------
            // DO NOT DELETE THE ACKNOWLEDGMENT.
            //
            // Keep the rejection note so the Admin/Auditor can still
            // see why it was rejected.
            //
            // Change status to Pending so the Auditor can review again.
            // ---------------------------------------------------------
            ack.Status = "Pending";

            try
            {
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message =
                        "Expenditure updated and resubmitted for auditor review.",

                    expenditureId = expenditure.Id,

                    status = "Pending",

                    previousRejectionNote = ack.Notes
                });
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    $"Update failed: {ex.Message}"
                );
            }
        }

        // ─────────────────────────────────────────────────────────────
        // ACKNOWLEDGE / REVIEW EXPENDITURE
        // AUDITOR ONLY
        //
        // Possible decisions:
        //
        // Approved
        // Rejected
        //
        // Rejected MUST have a reason.
        // ─────────────────────────────────────────────────────────────
        [HttpPut("acknowledge/{id}")]
        public async Task<IActionResult> AcknowledgeExpenditure(
            int id,
            [FromBody] AcknowledgeExpenditureDto dto)
        {
            if (dto == null)
                return BadRequest("Acknowledgment data is required.");

            if (string.IsNullOrWhiteSpace(dto.AuditorName))
                return BadRequest("AuditorName is required.");

            var expenditure = await _context.tblExpenditures
                .Include(e => e.Acknowledgments)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (expenditure == null)
                return NotFound("Expenditure not found.");

            var status = dto.Status?.Trim();

            // ---------------------------------------------------------
            // ONLY THESE TWO DECISIONS ARE ALLOWED
            // ---------------------------------------------------------
            var validStatuses = new[]
            {
                "Approved",
                "Rejected"
            };

            if (string.IsNullOrWhiteSpace(status) ||
                !validStatuses.Contains(status))
            {
                return BadRequest(
                    "Status must be 'Approved' or 'Rejected'."
                );
            }

            var notes = dto.Notes?.Trim();

            // ---------------------------------------------------------
            // REJECTION REQUIRES A REASON
            // ---------------------------------------------------------
            if (status == "Rejected" && string.IsNullOrWhiteSpace(notes))
            {
                return BadRequest(
                    "A rejection reason is required when rejecting an expenditure."
                );
            }

            var ack = expenditure.Acknowledgments.FirstOrDefault();

            // ---------------------------------------------------------
            // IF THIS EXPENDITURE HAS NEVER BEEN REVIEWED
            // CREATE ACKNOWLEDGMENT
            // ---------------------------------------------------------
            if (ack == null)
            {
                ack = new AuditorAcknowledgment
                {
                    ExpenditureId = id,
                    AuditorName = dto.AuditorName.Trim(),
                    AuditorEmail =
                        dto.AuditorEmail?.Trim() ?? string.Empty,
                    Status = status,
                    Notes = notes,
                    AcknowledgedAt = DateTime.Now
                };

                _context.tblAcknowledgments.Add(ack);
            }
            else
            {
                // -----------------------------------------------------
                // EXISTING ACKNOWLEDGMENT
                //
                // This normally happens when Admin has edited a
                // rejected expenditure and it is now Pending.
                //
                // Update the same acknowledgment instead of creating
                // a duplicate record.
                // -----------------------------------------------------

                if (ack.Status == "Approved")
                {
                    return BadRequest(
                        "This expenditure has already been approved."
                    );
                }

                if (ack.Status == "Rejected")
                {
                    return BadRequest(
                        "This expenditure is already rejected and must be edited and resubmitted by the Admin first."
                    );
                }

                if (ack.Status != "Pending")
                {
                    return BadRequest(
                        "This expenditure is not available for review."
                    );
                }

                ack.AuditorName = dto.AuditorName.Trim();

                ack.AuditorEmail =
                    dto.AuditorEmail?.Trim() ?? string.Empty;

                ack.Status = status;

                ack.Notes = notes;

                ack.AcknowledgedAt = DateTime.Now;
            }

            try
            {
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    ack.Id,
                    ack.ExpenditureId,
                    ack.AuditorName,
                    ack.AuditorEmail,
                    ack.Status,
                    ack.Notes,
                    ack.AcknowledgedAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    $"Acknowledge failed: {ex.Message}"
                );
            }
        }

        // ─────────────────────────────────────────────────────────────
        // SUPER ADMIN EDIT EXPENDITURE
        // Can edit ANY expenditure regardless of status.
        //
        // PUT /api/expenditures/superadmin-edit/{id}
        // ─────────────────────────────────────────────────────────────
        [Authorize(Roles = "superadmin")]
        [HttpPut("superadmin-edit/{id}")]
        public async Task<IActionResult> SuperAdminEditExpenditure(
            int id,
            [FromBody] SuperAdminExpenditureDto dto)
        {
            if (dto == null)
                return BadRequest("Expenditure data is required.");

            if (string.IsNullOrWhiteSpace(dto.Type))
                return BadRequest("Type is required.");

            if (dto.Amount <= 0)
                return BadRequest("Amount must be greater than zero.");

            if (dto.Date == default)
                return BadRequest("Date is required.");

            var expenditure = await _context.tblExpenditures
                .Include(e => e.Acknowledgments)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (expenditure == null)
                return NotFound("Expenditure not found.");

            expenditure.Type = dto.Type.Trim();
            expenditure.Amount = dto.Amount;

            expenditure.Description =
                string.IsNullOrWhiteSpace(dto.Description)
                    ? null
                    : dto.Description.Trim();

            expenditure.Date = dto.Date;

            try
            {
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Expenditure updated successfully by Super Admin.",
                    expenditureId = expenditure.Id,
                    expenditure.Type,
                    expenditure.Amount,
                    expenditure.Description,
                    expenditure.Date
                });
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    $"Update failed: {ex.Message}"
                );
            }
        }


        // ─────────────────────────────────────────────────────────────
        // SUPER ADMIN DELETE EXPENDITURE
        // Can delete ANY expenditure regardless of status.
        //
        // DELETE /api/expenditures/superadmin-delete/{id}
        // ─────────────────────────────────────────────────────────────
        [Authorize(Roles = "superadmin")]
        [HttpDelete("superadmin-delete/{id}")]
        public async Task<IActionResult> SuperAdminDeleteExpenditure(
            int id)
        {
            var expenditure = await _context.tblExpenditures
                .Include(e => e.Acknowledgments)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (expenditure == null)
                return NotFound("Expenditure not found.");

            try
            {
                // Remove acknowledgment records first to avoid
                // foreign-key constraint errors when cascade delete
                // is not configured.
                if (expenditure.Acknowledgments != null &&
                    expenditure.Acknowledgments.Any())
                {
                    _context.tblAcknowledgments.RemoveRange(
                        expenditure.Acknowledgments
                    );
                }

                _context.tblExpenditures.Remove(expenditure);

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message =
                        "Expenditure deleted successfully by Super Admin."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    $"Delete failed: {ex.Message}"
                );
            }
        }


        // ─────────────────────────────────────────────────────────────
        // GET ALL ACKNOWLEDGMENT RECORDS
        // AUDIT TRAIL
        // ─────────────────────────────────────────────────────────────
        [HttpGet("acknowledgments")]
        public async Task<IActionResult> GetAcknowledgments()
        {
            var acks = await _context.tblAcknowledgments
                .OrderByDescending(a => a.AcknowledgedAt)
                .ToListAsync();

            var result = acks.Select(a => new
            {
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

    // ─────────────────────────────────────────────────────────────────
    // DTOs
    // ─────────────────────────────────────────────────────────────────

    public class AcknowledgeExpenditureDto
    {
        public string AuditorName { get; set; } = string.Empty;

        public string? AuditorEmail { get; set; }

        // Approved | Rejected
        public string Status { get; set; } = "Approved";

        // Required when Status = Rejected
        public string? Notes { get; set; }
    }

    public class AcknowledgeBatchDto
    {
        public string AuditorName { get; set; } = string.Empty;

        public string? AuditorEmail { get; set; }

        // Approved | Rejected
        public string Status { get; set; } = "Approved";

        public string? Notes { get; set; }

        public DateTime PeriodFrom { get; set; }

        public DateTime PeriodTo { get; set; }
    }


    // ─────────────────────────────────────────────────────────────────
    // DTO FOR SUPER ADMIN EDIT
    // ─────────────────────────────────────────────────────────────────
    public class SuperAdminExpenditureDto
    {
        public string Type { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public string? Description { get; set; }

        public DateTime Date { get; set; }
    }
}