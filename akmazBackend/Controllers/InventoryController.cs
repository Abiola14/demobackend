// Controllers/InventoryController.cs
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

        [HttpGet]
        public async Task<ActionResult<IEnumerable<FishInventory>>> GetInventory()
        {
            return await _context.tblInventory
                .OrderBy(i => i.Name)
                .ToListAsync();
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddFish([FromBody] FishInventory fish)
        {
            if (fish == null || string.IsNullOrEmpty(fish.Name) || string.IsNullOrEmpty(fish.Quantity))
                return BadRequest("Fish Name and Quantity are required.");

            int qty = FishInventory.ExtractQuantity(fish.Quantity);
            if (qty <= 0) return BadRequest("Quantity must contain a valid number (e.g., 10 kg).");
            if (fish.Price <= 0) return BadRequest("Price must be greater than 0.");

            // AUTO-CALCULATE TotalPrice
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
                return StatusCode(500, $"Database error: {ex.Message}");
            }
        }

        [HttpPut("update/{id}")]
        public async Task<IActionResult> UpdateFish(int id, [FromBody] FishInventory updatedFish)
        {
            var fish = await _context.tblInventory.FindAsync(id);
            if (fish == null) return NotFound("Fish not found.");

            int qty = FishInventory.ExtractQuantity(updatedFish.Quantity);
            if (qty <= 0) return BadRequest("Invalid quantity format.");
            if (updatedFish.Price <= 0) return BadRequest("Price must be greater than 0.");

            fish.Name = updatedFish.Name;
            fish.Quantity = updatedFish.Quantity;
            fish.Price = updatedFish.Price;
            fish.Supplier = updatedFish.Supplier;

            // RECALCULATE TotalPrice
            fish.TotalPrice = updatedFish.Price * qty;

            fish.Modified_At = DateTime.Now;
            fish.Modified_By = updatedFish.Modified_By ?? "Admin";

            try
            {
                await _context.SaveChangesAsync();
                return Ok(fish);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Update failed: {ex.Message}");
            }
        }
    }
}