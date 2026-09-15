// Controllers/BankDepositsController.cs

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AkmazBackend.Data;
using AkmazBackend.Models;

namespace AkmazBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BankDepositsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BankDepositsController(AppDbContext context)
        {
            _context = context;
        }

        // ── GET all deposits ──────────────────────────────────────────
        // GET /api/bankdeposits
        [HttpGet]
        public async Task<ActionResult<IEnumerable<BankDeposit>>> GetDeposits()
        {
            return await _context.tblBankDeposits
                .OrderByDescending(d => d.Date)
                .ToListAsync();
        }


        // ── ADD a new deposit (Admin) ─────────────────────────────────
        // POST /api/bankdeposits/add
        [HttpPost("add")]
        public async Task<IActionResult> AddDeposit(
            [FromBody] BankDeposit deposit)
        {
            if (deposit == null)
                return BadRequest("Deposit data is required.");

            if (deposit.Amount <= 0)
                return BadRequest("Amount must be greater than zero.");

            if (string.IsNullOrWhiteSpace(deposit.CreatedBy))
                return BadRequest("CreatedBy is required.");

            deposit.Date =
                deposit.Date == default
                    ? DateTime.Now
                    : deposit.Date;

            // New deposits always start as pending
            deposit.IsConfirmed = false;
            deposit.ConfirmedBy = null;
            deposit.ConfirmedAt = null;

            try
            {
                _context.tblBankDeposits.Add(deposit);

                await _context.SaveChangesAsync();

                return Ok(deposit);
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    $"Database error: {ex.Message}"
                );
            }
        }


        // ── CONFIRM a deposit (Auditor) ───────────────────────────────
        // PUT /api/bankdeposits/confirm/{id}
        [HttpPut("confirm/{id}")]
        public async Task<IActionResult> ConfirmDeposit(
            int id,
            [FromBody] ConfirmDepositDto dto)
        {
            if (dto == null ||
                string.IsNullOrWhiteSpace(dto.ConfirmedBy))
            {
                return BadRequest("ConfirmedBy is required.");
            }

            var deposit =
                await _context.tblBankDeposits.FindAsync(id);

            if (deposit == null)
                return NotFound("Deposit not found.");

            if (deposit.IsConfirmed)
            {
                return BadRequest(
                    "This deposit has already been confirmed."
                );
            }

            deposit.IsConfirmed = true;
            deposit.ConfirmedBy = dto.ConfirmedBy.Trim();
            deposit.ConfirmedAt = DateTime.Now;

            try
            {
                await _context.SaveChangesAsync();

                return Ok(deposit);
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    $"Confirm failed: {ex.Message}"
                );
            }
        }


        // ── DELETE a deposit (Admin, unconfirmed only) ────────────────
        // DELETE /api/bankdeposits/delete/{id}
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteDeposit(int id)
        {
            var deposit =
                await _context.tblBankDeposits.FindAsync(id);

            if (deposit == null)
                return NotFound("Deposit not found.");

            // Admin cannot delete confirmed deposits
            if (deposit.IsConfirmed)
            {
                return BadRequest(
                    "Cannot delete a confirmed deposit."
                );
            }

            try
            {
                _context.tblBankDeposits.Remove(deposit);

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Deposit deleted successfully."
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


        // =============================================================
        // SUPER ADMIN
        // =============================================================


        // ── SUPER ADMIN EDIT DEPOSIT ──────────────────────────────────
        // PUT /api/bankdeposits/superadmin-edit/{id}
        //
        // Super Admin can edit ANY deposit,
        // including confirmed deposits.
        //
        // Confirmation status/history is preserved.
        // =============================================================

        [Authorize(Roles = "superadmin")]
        [HttpPut("superadmin-edit/{id}")]
        public async Task<IActionResult> SuperAdminEditDeposit(
            int id,
            [FromBody] SuperAdminBankDepositDto dto)
        {
            if (dto == null)
                return BadRequest("Deposit data is required.");

            if (dto.Amount <= 0)
                return BadRequest(
                    "Amount must be greater than zero."
                );

            if (dto.Date == default)
                return BadRequest("Date is required.");

            var deposit =
                await _context.tblBankDeposits.FindAsync(id);

            if (deposit == null)
                return NotFound("Deposit not found.");

            // Super Admin can correct:
            // - Amount
            // - Description
            // - Date
            //
            // Confirmation information is deliberately preserved.

            deposit.Amount = dto.Amount;

            deposit.Description =
                string.IsNullOrWhiteSpace(dto.Description)
                    ? null
                    : dto.Description.Trim();

            deposit.Date = dto.Date;

            try
            {
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Deposit updated successfully.",
                    deposit
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


        // ── SUPER ADMIN DELETE DEPOSIT ────────────────────────────────
        // DELETE /api/bankdeposits/superadmin-delete/{id}
        //
        // Super Admin can delete ANY deposit,
        // including confirmed deposits.
        // =============================================================

        [Authorize(Roles = "superadmin")]
        [HttpDelete("superadmin-delete/{id}")]
        public async Task<IActionResult> SuperAdminDeleteDeposit(
            int id)
        {
            var deposit =
                await _context.tblBankDeposits.FindAsync(id);

            if (deposit == null)
                return NotFound("Deposit not found.");

            try
            {
                _context.tblBankDeposits.Remove(deposit);

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message =
                        "Deposit deleted successfully by Super Admin."
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
    }


    // ================================================================
    // DTO FOR CONFIRM ENDPOINT
    // ================================================================

    public class ConfirmDepositDto
    {
        public string ConfirmedBy { get; set; } = string.Empty;
    }


    // ================================================================
    // DTO FOR SUPER ADMIN EDIT
    // ================================================================

    public class SuperAdminBankDepositDto
    {
        public decimal Amount { get; set; }

        public string? Description { get; set; }

        public DateTime Date { get; set; }
    }
}