using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AkmazBackend.Data;
using AkmazBackend.Models;

namespace AkmazBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InventoryController : ControllerBase
    {
        private readonly AppDbContext _context;

        public InventoryController(AppDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // GET INVENTORY
        // ============================================================

        [HttpGet]
        public async Task<ActionResult<IEnumerable<FishInventory>>> GetInventory()
        {
            return await _context.tblInventory
                .OrderBy(i => i.Name)
                .ToListAsync();
        }

        // ============================================================
        // ADD INVENTORY
        // ============================================================

        [HttpPost("add")]
        public async Task<IActionResult> AddFish(
            [FromBody] FishInventory fish)
        {
            if (
                fish == null ||
                string.IsNullOrEmpty(fish.Name) ||
                string.IsNullOrEmpty(fish.Quantity)
            )
            {
                return BadRequest(
                    "Fish Name and Quantity are required."
                );
            }

            int qty = FishInventory.ExtractQuantity(
                fish.Quantity
            );

            if (qty <= 0)
            {
                return BadRequest(
                    "Quantity must contain a valid number (e.g., 10 kg)."
                );
            }

            if (fish.Price <= 0)
            {
                return BadRequest(
                    "Price must be greater than 0."
                );
            }

            // AUTO-CALCULATE TOTAL PRICE
            fish.TotalPrice = fish.Price * qty;

            fish.Created_At = DateTime.Now;

            fish.Created_By ??= "Admin";

            try
            {
                _context.tblInventory.Add(fish);

                await _context.SaveChangesAsync();

                return Ok(fish);
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    $"Database error: {ex.Message}"
                );
            }
        }

        // ============================================================
        // NORMAL ADMIN UPDATE
        // ============================================================

        [HttpPut("update/{id}")]
        public async Task<IActionResult> UpdateFish(
            int id,
            [FromBody] FishInventory updatedFish)
        {
            var fish = await _context.tblInventory.FindAsync(id);

            if (fish == null)
            {
                return NotFound("Fish not found.");
            }

            int qty = FishInventory.ExtractQuantity(
                updatedFish.Quantity
            );

            if (qty <= 0)
            {
                return BadRequest(
                    "Invalid quantity format."
                );
            }

            if (updatedFish.Price <= 0)
            {
                return BadRequest(
                    "Price must be greater than 0."
                );
            }

            fish.Name = updatedFish.Name;
            fish.Quantity = updatedFish.Quantity;
            fish.Price = updatedFish.Price;
            fish.Supplier = updatedFish.Supplier;

            // RECALCULATE TOTAL PRICE
            fish.TotalPrice =
                updatedFish.Price * qty;

            fish.Modified_At = DateTime.Now;

            fish.Modified_By =
                updatedFish.Modified_By ?? "Admin";

            try
            {
                await _context.SaveChangesAsync();

                return Ok(fish);
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    $"Update failed: {ex.Message}"
                );
            }
        }

        // ============================================================
        // SUPER ADMIN UPDATE
        // ============================================================

        [Authorize(Roles = "superadmin")]
        [HttpPut("superadmin-edit/{id}")]
        public async Task<IActionResult> SuperAdminEdit(
            int id,
            [FromBody] FishInventory updatedFish)
        {
            var fish = await _context.tblInventory.FindAsync(id);

            if (fish == null)
            {
                return NotFound("Inventory record not found.");
            }

            if (
                updatedFish == null ||
                string.IsNullOrWhiteSpace(updatedFish.Name) ||
                string.IsNullOrWhiteSpace(updatedFish.Quantity)
            )
            {
                return BadRequest(
                    "Fish Name and Quantity are required."
                );
            }

            int qty = FishInventory.ExtractQuantity(
                updatedFish.Quantity
            );

            if (qty <= 0)
            {
                return BadRequest(
                    "Quantity must contain a valid number."
                );
            }

            if (updatedFish.Price <= 0)
            {
                return BadRequest(
                    "Price must be greater than 0."
                );
            }

            fish.Name = updatedFish.Name.Trim();

            fish.Quantity =
                updatedFish.Quantity.Trim();

            fish.Price = updatedFish.Price;

            fish.Supplier =
                updatedFish.Supplier?.Trim();

            // RECALCULATE TOTAL
            fish.TotalPrice =
                updatedFish.Price * qty;

            // SUPER ADMIN AUDIT
            fish.Modified_At = DateTime.Now;

            fish.Modified_By = "Super Admin";

            try
            {
                await _context.SaveChangesAsync();

                return Ok(fish);
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    $"Super Admin update failed: {ex.Message}"
                );
            }
        }

        // ============================================================
        // SUPER ADMIN DELETE
        // ============================================================

        [Authorize(Roles = "superadmin")]
        [HttpDelete("superadmin-delete/{id}")]
        public async Task<IActionResult> SuperAdminDelete(
            int id)
        {
            var fish = await _context.tblInventory.FindAsync(id);

            if (fish == null)
            {
                return NotFound(
                    "Inventory record not found."
                );
            }

            try
            {
                _context.tblInventory.Remove(fish);

                await _context.SaveChangesAsync();

                return Ok(
                    new
                    {
                        message =
                            "Inventory record deleted successfully."
                    }
                );
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
}