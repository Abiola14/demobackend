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
    [Authorize]
    public class SalesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SalesController(AppDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // GET ALL SALES
        // ============================================================
        // Both Admin and Auditor can see all sales.
        // Unpaid sales appear first.
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Sale>>> GetSales()
        {
            var sales = await _context.tblSales
                .OrderBy(s => s.PaymentStatus == "Unpaid" ? 0 : 1)
                .ThenByDescending(s => s.SoldAt)
                .ToListAsync();

            return Ok(sales);
        }


        // ============================================================
        // ADD SALE - ADMIN ONLY
        // ============================================================
        [Authorize(Roles = "admin")]
        [HttpPost("add")]
        public async Task<IActionResult> AddSale([FromBody] SaleRequest dto)
        {
            if (dto == null)
                return BadRequest("Invalid sale data.");

            // Validate required fields
            if (string.IsNullOrWhiteSpace(dto.Product))
                return BadRequest("Product is required.");

            if (string.IsNullOrWhiteSpace(dto.CustomerName))
                return BadRequest("Customer name is required.");

            if (string.IsNullOrWhiteSpace(dto.Quantity))
                return BadRequest("Quantity is required.");

            // Extract numeric quantity
            int qty = Sale.ExtractQuantity(dto.Quantity);

            if (qty <= 0)
                return BadRequest("Invalid quantity.");

            if (dto.UnitPrice <= 0)
                return BadRequest("Invalid unit price.");


            // ========================================================
            // PAYMENT STATUS
            // ========================================================
            var status = dto.PaymentStatus?.Trim();

            if (status != "Paid" && status != "Unpaid")
            {
                status = "Unpaid";
            }


            // ========================================================
            // CURRENT USER
            // ========================================================
            var username =
                User.FindFirstValue(ClaimTypes.Name)
                ?? User.FindFirstValue("name")
                ?? User.FindFirstValue("username")
                ?? "admin";


            // ========================================================
            // CREATE SALE
            // ========================================================
            var sale = new Sale
            {
                Product = dto.Product.Trim(),

                CustomerName = dto.CustomerName.Trim(),

                Quantity = dto.Quantity.Trim(),

                UnitPrice = dto.UnitPrice,

                TotalPrice = dto.UnitPrice * qty,

                CreatedBy = username,

                SoldAt = DateTime.UtcNow,

                ModifiedBy = null,

                ModifiedAt = null,

                PaymentStatus = status,

                // If Admin creates it as Paid,
                // the Admin is the person who marked it Paid.
                ConfirmedBy = status == "Paid"
                    ? username
                    : null,

                ConfirmedAt = status == "Paid"
                    ? DateTime.UtcNow
                    : null
            };


            _context.tblSales.Add(sale);

            await _context.SaveChangesAsync();

            return Ok(sale);
        }


        // ============================================================
        // UPDATE SALE - ADMIN ONLY
        // ============================================================
        [Authorize(Roles = "admin")]
        [HttpPut("update/{id}")]
        public async Task<IActionResult> UpdateSale(
            int id,
            [FromBody] SaleRequest dto)
        {
            if (dto == null)
                return BadRequest("Invalid sale data.");


            var sale = await _context.tblSales.FindAsync(id);

            if (sale == null)
                return NotFound("Sale not found.");


            // Validate quantity
            int qty = Sale.ExtractQuantity(dto.Quantity);

            if (qty <= 0)
                return BadRequest("Invalid quantity.");

            if (dto.UnitPrice <= 0)
                return BadRequest("Invalid unit price.");


            // ========================================================
            // CURRENT USER
            // ========================================================
            var username =
                User.FindFirstValue(ClaimTypes.Name)
                ?? User.FindFirstValue("name")
                ?? User.FindFirstValue("username")
                ?? "admin";


            // ========================================================
            // UPDATE SALE INFORMATION
            // ========================================================
            sale.Product =
                dto.Product?.Trim() ?? sale.Product;

            sale.CustomerName =
                dto.CustomerName?.Trim() ?? sale.CustomerName;

            sale.Quantity =
                dto.Quantity?.Trim() ?? sale.Quantity;

            sale.UnitPrice = dto.UnitPrice;

            sale.TotalPrice = dto.UnitPrice * qty;


            // ========================================================
            // PAYMENT STATUS
            // ========================================================
            var newStatus = dto.PaymentStatus?.Trim();

            if (newStatus == "Paid" || newStatus == "Unpaid")
            {
                sale.PaymentStatus = newStatus;
            }


            // ========================================================
            // MODIFICATION AUDIT
            // ========================================================
            sale.ModifiedBy = username;
            sale.ModifiedAt = DateTime.UtcNow;


            // ========================================================
            // IF ADMIN CHANGES STATUS TO PAID
            // ========================================================
            if (sale.PaymentStatus == "Paid" &&
                string.IsNullOrWhiteSpace(sale.ConfirmedBy))
            {
                sale.ConfirmedBy = username;
                sale.ConfirmedAt = DateTime.UtcNow;
            }


            // ========================================================
            // IF STATUS IS CHANGED BACK TO UNPAID
            // ========================================================
            if (sale.PaymentStatus == "Unpaid")
            {
                sale.ConfirmedBy = null;
                sale.ConfirmedAt = null;
            }


            await _context.SaveChangesAsync();

            return Ok(sale);
        }


        // ============================================================
        // SUPER ADMIN EDIT SALE
        // ============================================================
        // Super Admin can edit ANY sale.
        //
        // PUT /api/sales/superadmin-edit/{id}
        // ============================================================
        [Authorize(Roles = "superadmin")]
        [HttpPut("superadmin-edit/{id}")]
        public async Task<IActionResult> SuperAdminEditSale(
            int id,
            [FromBody] SaleRequest dto)
        {
            if (dto == null)
                return BadRequest("Invalid sale data.");

            if (string.IsNullOrWhiteSpace(dto.Product))
                return BadRequest("Product is required.");

            if (string.IsNullOrWhiteSpace(dto.CustomerName))
                return BadRequest("Customer name is required.");

            if (string.IsNullOrWhiteSpace(dto.Quantity))
                return BadRequest("Quantity is required.");

            int qty = Sale.ExtractQuantity(dto.Quantity);

            if (qty <= 0)
                return BadRequest("Invalid quantity.");

            if (dto.UnitPrice <= 0)
                return BadRequest("Invalid unit price.");

            var sale = await _context.tblSales.FindAsync(id);

            if (sale == null)
                return NotFound("Sale not found.");

            sale.Product = dto.Product.Trim();

            sale.CustomerName = dto.CustomerName.Trim();

            sale.Quantity = dto.Quantity.Trim();

            sale.UnitPrice = dto.UnitPrice;

            sale.TotalPrice = dto.UnitPrice * qty;

            // Payment status can also be corrected by Super Admin.
            var newStatus = dto.PaymentStatus?.Trim();

            if (newStatus == "Paid" || newStatus == "Unpaid")
            {
                sale.PaymentStatus = newStatus;
            }

            // Get authenticated Super Admin username.
            var username =
                User.FindFirstValue(ClaimTypes.Name)
                ?? User.FindFirstValue("name")
                ?? User.FindFirstValue("username")
                ?? "admin";

            sale.ModifiedBy = username;
            sale.ModifiedAt = DateTime.UtcNow;

            // If changed to Paid and there is no confirmation record,
            // record the Super Admin as the person who marked it Paid.
            if (sale.PaymentStatus == "Paid" &&
                string.IsNullOrWhiteSpace(sale.ConfirmedBy))
            {
                sale.ConfirmedBy = username;
                sale.ConfirmedAt = DateTime.UtcNow;
            }

            // If changed back to Unpaid, clear payment confirmation.
            if (sale.PaymentStatus == "Unpaid")
            {
                sale.ConfirmedBy = null;
                sale.ConfirmedAt = null;
            }

            try
            {
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Sale updated successfully by Super Admin.",
                    sale
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


        // ============================================================
        // SUPER ADMIN DELETE SALE
        // ============================================================
        // Super Admin can delete ANY sale.
        //
        // DELETE /api/sales/superadmin-delete/{id}
        // ============================================================
        [Authorize(Roles = "superadmin")]
        [HttpDelete("superadmin-delete/{id}")]
        public async Task<IActionResult> SuperAdminDeleteSale(int id)
        {
            var sale = await _context.tblSales.FindAsync(id);

            if (sale == null)
                return NotFound("Sale not found.");

            try
            {
                _context.tblSales.Remove(sale);

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message =
                        "Sale deleted successfully by Super Admin."
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


        // ============================================================
        // CONFIRM PAYMENT - AUDITOR ONLY
        // ============================================================
        [Authorize(Roles = "auditor")]
        [HttpPut("confirm-payment/{id}")]
        public async Task<IActionResult> ConfirmPayment(int id)
        {
            // ========================================================
            // CURRENT USER
            // ========================================================
            var username =
                User.FindFirstValue(ClaimTypes.Name)
                ?? User.FindFirstValue("name")
                ?? User.FindFirstValue("username");


            if (string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized(
                    "No authenticated user found."
                );
            }


            // ========================================================
            // FIND SALE
            // ========================================================
            var sale = await _context.tblSales.FindAsync(id);

            if (sale == null)
            {
                return NotFound(
                    $"Sale with ID {id} not found."
                );
            }


            // ========================================================
            // MAKE SURE SALE IS UNPAID
            // ========================================================
            if (sale.PaymentStatus != "Unpaid")
            {
                return BadRequest(
                    $"Cannot confirm: Payment is already {sale.PaymentStatus}."
                );
            }


            // ========================================================
            // CONFIRM PAYMENT
            // ========================================================
            sale.PaymentStatus = "Paid";

            sale.ConfirmedBy = username;

            sale.ConfirmedAt = DateTime.UtcNow;


            await _context.SaveChangesAsync();


            return Ok(new
            {
                message = $"Payment confirmed by {username}",

                confirmedBy = username,

                confirmedAt =
                    sale.ConfirmedAt?
                        .ToString("yyyy-MM-dd HH:mm:ss UTC"),

                saleId = sale.Id
            });
        }
    }


    // ================================================================
    // REQUEST DTO
    // ================================================================
    public class SaleRequest
    {
        public string Product { get; set; } = string.Empty;

        public string CustomerName { get; set; } = string.Empty;

        public string Quantity { get; set; } = string.Empty;

        public decimal UnitPrice { get; set; }

        public string PaymentStatus { get; set; } = "Unpaid";
    }
}