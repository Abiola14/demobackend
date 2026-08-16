using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AkmazBackend.Data;
using AkmazBackend.Models;
using System.Security.Claims;

namespace AkmazBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // ✅ All endpoints require a valid JWT by default
    public class SalesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SalesController(AppDbContext context)
        {
            _context = context;
        }

        // ================= GET ALL SALES =================
        // ✅ Both admin AND auditor see ALL sales (Paid + Unpaid)
        // Auditor can only ACTION on Unpaid ones — enforced in the frontend + confirm endpoint
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Sale>>> GetSales()
        {
            var sales = await _context.tblSales
                .OrderBy(s => s.PaymentStatus == "Unpaid" ? 0 : 1) // Unpaid first
                .ThenByDescending(s => s.SoldAt)
                .ToListAsync();

            return Ok(sales);
        }

        // ================= ADD SALE (ADMIN ONLY) =================
        [Authorize(Roles = "admin")]
        [HttpPost("add")]
        public async Task<IActionResult> AddSale([FromBody] SaleRequest dto)
        {
            if (dto == null)
                return BadRequest("Invalid sale data.");

            int qty = Sale.ExtractQuantity(dto.Quantity);
            if (qty <= 0 || dto.UnitPrice <= 0)
                return BadRequest("Invalid quantity or price.");

            // ✅ Validate payment status
            var status = dto.PaymentStatus?.Trim();
            if (status != "Paid" && status != "Unpaid")
                status = "Unpaid";

            var sale = new Sale
            {
                Product = dto.Product.Trim(),
                CustomerName = dto.CustomerName.Trim(),
                Quantity = dto.Quantity.Trim(),
                UnitPrice = dto.UnitPrice,
                TotalPrice = dto.UnitPrice * qty,
                PaymentStatus = status,
                SoldAt = DateTime.UtcNow,
                CreatedBy = User.FindFirstValue(ClaimTypes.Name) ?? "admin",
                ConfirmedBy = null,
                ConfirmedAt = null
            };

            _context.tblSales.Add(sale);
            await _context.SaveChangesAsync();

            return Ok(sale);
        }

        // ================= UPDATE SALE (ADMIN ONLY) =================
        [Authorize(Roles = "admin")]
        [HttpPut("update/{id}")]
        public async Task<IActionResult> UpdateSale(int id, [FromBody] SaleRequest dto)
        {
            var sale = await _context.tblSales.FindAsync(id);
            if (sale == null)
                return NotFound("Sale not found.");

            int qty = Sale.ExtractQuantity(dto.Quantity);
            if (qty <= 0 || dto.UnitPrice <= 0)
                return BadRequest("Invalid quantity or price.");

            sale.Product = dto.Product?.Trim() ?? sale.Product;
            sale.CustomerName = dto.CustomerName?.Trim() ?? sale.CustomerName;
            sale.Quantity = dto.Quantity?.Trim() ?? sale.Quantity;
            sale.UnitPrice = dto.UnitPrice;
            sale.TotalPrice = dto.UnitPrice * qty;
            sale.ModifiedBy = User.FindFirstValue(ClaimTypes.Name) ?? "admin";
            sale.ModifiedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(sale);
        }

        // ================= CONFIRM PAYMENT (AUDITOR ONLY) =================
        // ✅ No body expected. Role is verified by [Authorize(Roles = "auditor")].
        // No need to re-query the user from DB — the JWT claim is enough.
        [Authorize(Roles = "auditor")]
        [HttpPut("confirm-payment/{id}")]
        public async Task<IActionResult> ConfirmPayment(int id)
        {
            var username = User.FindFirstValue(ClaimTypes.Name);
            if (string.IsNullOrWhiteSpace(username))
                return Unauthorized("No authenticated user found.");

            var sale = await _context.tblSales.FindAsync(id);
            if (sale == null)
                return NotFound($"Sale with ID {id} not found.");

            if (sale.PaymentStatus != "Unpaid")
                return BadRequest($"Cannot confirm: Payment is already {sale.PaymentStatus}.");

            sale.PaymentStatus = "Paid";
            sale.ConfirmedBy = username;
            sale.ConfirmedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = $"Payment confirmed by {username}",
                confirmedAt = sale.ConfirmedAt?.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                saleId = sale.Id
            });
        }
    }

    // ================= REQUEST DTO =================
    // ✅ Separate DTO so the frontend sends clean camelCase JSON
    // and we never expose internal model fields to the API consumer
    public class SaleRequest
    {
        public string Product { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Quantity { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public string PaymentStatus { get; set; } = "Unpaid";
    }
}
