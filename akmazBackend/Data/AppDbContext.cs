// Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using AkmazBackend.Models;

namespace AkmazBackend.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        // ── Tables ──────────────────────────────────────────────────
        public DbSet<User> tblUsers => Set<User>();
        public DbSet<Sale> tblSales => Set<Sale>();
        public DbSet<FishInventory> tblInventory => Set<FishInventory>();
        public DbSet<BankDeposit> tblBankDeposits => Set<BankDeposit>();
        public DbSet<Expenditure> tblExpenditures => Set<Expenditure>();
        public DbSet<AuditorAcknowledgment> tblAcknowledgments => Set<AuditorAcknowledgment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── Table name mappings ──────────────────────────────────
            modelBuilder.Entity<User>()
                .ToTable("tblUsers");

            modelBuilder.Entity<Sale>()
                .ToTable("tblSales");

            modelBuilder.Entity<FishInventory>()
                .ToTable("tblInventory");

            // ✅ CHANGED: BankDeposits → tblBankDeposits
            modelBuilder.Entity<BankDeposit>()
                .ToTable("tblBankDeposits");

            // ✅ CHANGED: Expenditures → tblExpenditures
            modelBuilder.Entity<Expenditure>()
                .ToTable("tblExpenditures");

            modelBuilder.Entity<AuditorAcknowledgment>()
                .ToTable("tblAuditorAcknowledgments");


            // ── Primary Keys ─────────────────────────────────────────
            modelBuilder.Entity<User>()
                .HasKey(u => u.Id);

            modelBuilder.Entity<Sale>()
                .HasKey(s => s.Id);

            modelBuilder.Entity<FishInventory>()
                .HasKey(i => i.Id);

            modelBuilder.Entity<BankDeposit>()
                .HasKey(b => b.Id);

            modelBuilder.Entity<Expenditure>()
                .HasKey(e => e.Id);

            modelBuilder.Entity<AuditorAcknowledgment>()
                .HasKey(a => a.Id);


            // ── Decimal precision for MySQL ───────────────────────────

            modelBuilder.Entity<Sale>()
                .Property(s => s.UnitPrice)
                .HasPrecision(14, 2);

            modelBuilder.Entity<Sale>()
                .Property(s => s.TotalPrice)
                .HasPrecision(14, 2);

            modelBuilder.Entity<FishInventory>()
                .Property(f => f.Price)
                .HasPrecision(14, 2);

            modelBuilder.Entity<FishInventory>()
                .Property(f => f.TotalPrice)
                .HasPrecision(14, 2);

            modelBuilder.Entity<BankDeposit>()
                .Property(b => b.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Expenditure>()
                .Property(e => e.Amount)
                .HasPrecision(18, 2);


            // ── Default values ────────────────────────────────────────

            modelBuilder.Entity<Sale>()
                .Property(s => s.SoldAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<BankDeposit>()
                .Property(b => b.IsConfirmed)
                .HasDefaultValue(false);

            modelBuilder.Entity<Expenditure>()
                .Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");


            // ── Nullable columns ─────────────────────────────────────

            modelBuilder.Entity<BankDeposit>()
                .Property(b => b.ConfirmedBy)
                .IsRequired(false);

            modelBuilder.Entity<BankDeposit>()
                .Property(b => b.ConfirmedAt)
                .IsRequired(false);

            modelBuilder.Entity<BankDeposit>()
                .Property(b => b.Description)
                .IsRequired(false);

            modelBuilder.Entity<Expenditure>()
                .Property(e => e.Description)
                .IsRequired(false);

            modelBuilder.Entity<Expenditure>()
                .Property(e => e.CreatedBy)
                .IsRequired(false);


            // ── Relationships ─────────────────────────────────────────

            // AuditorAcknowledgment → Expenditure
            modelBuilder.Entity<AuditorAcknowledgment>()
                .HasOne(a => a.Expenditure)
                .WithMany(e => e.Acknowledgments)
                .HasForeignKey(a => a.ExpenditureId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

            modelBuilder.Entity<AuditorAcknowledgment>()
                .Property(a => a.ExpenditureId)
                .IsRequired(false);

            modelBuilder.Entity<AuditorAcknowledgment>()
                .Property(a => a.Notes)
                .IsRequired(false);

            modelBuilder.Entity<AuditorAcknowledgment>()
                .Property(a => a.PeriodFrom)
                .IsRequired(false);

            modelBuilder.Entity<AuditorAcknowledgment>()
                .Property(a => a.PeriodTo)
                .IsRequired(false);
        }
    }
}