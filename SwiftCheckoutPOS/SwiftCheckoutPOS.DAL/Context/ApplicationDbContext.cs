// Project: SwiftCheckoutPOS.DAL
// File: Context/ApplicationDbContext.cs
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.DAL.Entities;

namespace SwiftCheckoutPOS.DAL.Context;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext()
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Category> Categories { get; set; }
    public virtual DbSet<Product> Products { get; set; }
    public virtual DbSet<Sale> Sales { get; set; }
    public virtual DbSet<SaleItem> SaleItems { get; set; }
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<Customer> Customers { get; set; } = null!;
    public virtual DbSet<Vendor> Vendors { get; set; }
    public virtual DbSet<TillReconciliation> TillReconciliations { get; set; }
    public virtual DbSet<LedgerEntry> LedgerEntries { get; set; }
    public virtual DbSet<SystemSetting> SystemSettings { get; set; }
    public virtual DbSet<AuditEvent> AuditEvents { get; set; } = null!;
    public virtual DbSet<SalePayment> SalePayments { get; set; } = null!;
    public virtual DbSet<ReceiptSequence> ReceiptSequences { get; set; } = null!;
    public virtual DbSet<CustomerDuePayment> CustomerDuePayments { get; set; } = null!;
    public virtual DbSet<Refund> Refunds { get; set; } = null!;
    public virtual DbSet<Shift> Shifts { get; set; } = null!;
    public virtual DbSet<CashMovement> CashMovements { get; set; } = null!;
    public virtual DbSet<RefundItem> RefundItems { get; set; } = null!;
    public virtual DbSet<RefundPayout> RefundPayouts { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // ARCHITECT FIX: Only use the named connection string if options haven't been provided 
        // by the Design-Time Factory or the UI layer's Dependency Injection.
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("Name=DbConn");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__Categori__19093A0B4E938104");
            entity.Property(e => e.CategoryName).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.HasIndex(e => e.CategoryName);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.ProductId).HasName("PK__Products__B40CC6CDBCFF5845");
            entity.HasIndex(e => e.Barcode, "UQ__Products__177800D302B1BAD5").IsUnique();
            entity.Property(e => e.Barcode).HasMaxLength(50);
            entity.Property(e => e.CostPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.ProductName).HasMaxLength(150);
            entity.Property(e => e.SellingPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxRate).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => new { e.IsActive, e.StockQuantity });
            entity.ToTable(t => t.HasCheckConstraint("CK_Products_Reserved", "[ReservedQuantity] >= 0 AND [ReservedQuantity] <= [StockQuantity]"));
            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Products__Catego__3C69FB99");
            entity.HasOne(d => d.Vendor).WithMany(p => p.Products)
                .HasForeignKey(d => d.VendorId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Products_Vendors_VendorId");
        });

        modelBuilder.Entity<Sale>(entity =>
        {
            entity.HasKey(e => e.SaleId).HasName("PK__Sales__1EE3C3FFEB3A7B6D");
            entity.HasIndex(e => e.ReceiptNumber, "UQ__Sales__C08AFDAB959AECE3").IsUnique();
            entity.Property(e => e.PaymentMethod).HasMaxLength(50);
            entity.Property(e => e.ReceiptNumber).HasMaxLength(50);
            entity.Property(e => e.SaleDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.SubTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.RoundingAmount).HasColumnType("decimal(18, 2)").HasDefaultValue(0m);
            entity.Property(e => e.OrderStatus).HasMaxLength(20);
            entity.Property(e => e.CashierName).HasMaxLength(100);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => new { e.SaleDate, e.OrderStatus });
            entity.Property(e => e.HeldAt).HasColumnType("datetime2");
            entity.HasIndex(e => new { e.OrderStatus, e.HeldAt });
            entity.HasIndex(e => e.OperationKey).IsUnique().HasFilter("[OperationKey] IS NOT NULL");
        });

        modelBuilder.Entity<SaleItem>(entity =>
        {
            entity.HasKey(e => e.SaleItemId).HasName("PK__SaleItem__C6059401FE4424A0");
            entity.Property(e => e.LineTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CostAtSale).HasColumnType("decimal(18, 2)");
            entity.HasOne(d => d.Product).WithMany(p => p.SaleItems)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__SaleItems__Produ__44FF419A");
            entity.HasOne(d => d.Sale).WithMany(p => p.SaleItems)
                .HasForeignKey(d => d.SaleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__SaleItems__SaleI__440B1D61");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4C587B2A00");
            entity.HasIndex(e => e.Username, "UQ__Users__536C85E4F504D648").IsUnique();
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.Username).HasMaxLength(50);
            entity.Property(e => e.SecurityStamp).HasDefaultValueSql("CONVERT(nvarchar(40), NEWID())");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.Property(e => e.FullName).HasMaxLength(150);
            entity.Property(e => e.PhoneNumber).HasMaxLength(30);
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.HasIndex(e => e.PhoneNumber);
        });

        modelBuilder.Entity<Vendor>(entity => entity.HasIndex(e => e.VendorName));

        modelBuilder.Entity<LedgerEntry>(entity =>
        {
            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Category).HasMaxLength(50);
            entity.Property(e => e.TransactionType).HasMaxLength(20);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.RecordedBy).HasMaxLength(100);
            entity.HasIndex(e => e.TransactionDate);
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.Property(e => e.LastUpdated).HasColumnType("datetime2");
        });

        modelBuilder.Entity<TillReconciliation>(entity =>
        {
            entity.Property(e => e.ExpectedCash).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CountedCash).HasColumnType("decimal(18, 2)");
            entity.HasIndex(e => e.CloseTime);
        });

        modelBuilder.Entity<SalePayment>(entity =>
        {
            entity.HasOne(e => e.Sale).WithMany(s => s.Payments).HasForeignKey(e => e.SaleId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t => t.HasCheckConstraint("CK_SalePayments_Amount", "[Amount] > 0"));
        });

        modelBuilder.Entity<Refund>(entity =>
        {
            entity.Property(e => e.RefundedAt).HasColumnType("datetime2");
            entity.HasOne(e => e.Sale).WithMany(s => s.Refunds).HasForeignKey(e => e.SaleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(e => e.Items).WithOne().HasForeignKey(i => i.RefundId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(e => e.Payouts).WithOne().HasForeignKey(i => i.RefundId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.RefundedAt);
        });
        modelBuilder.Entity<RefundItem>(entity =>
        {
            entity.HasOne<SaleItem>().WithMany().HasForeignKey(e => e.SaleItemId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t => t.HasCheckConstraint("CK_RefundItems_Quantity", "[Quantity] > 0"));
        });

        modelBuilder.Entity<Shift>(entity =>
        {
            entity.Property(e => e.OpenedAt).HasColumnType("datetime2");
            entity.Property(e => e.ClosedAt).HasColumnType("datetime2");
            // PR-Q42: only one shift open at a time.
            entity.HasIndex(e => e.Status).IsUnique().HasFilter("[Status] = N'Open'").HasDatabaseName("UX_Shifts_OneOpen");
            entity.HasMany(e => e.CashMovements).WithOne().HasForeignKey(m => m.ShiftId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CashMovement>(entity => entity.Property(e => e.At).HasColumnType("datetime2"));
        modelBuilder.Entity<Sale>().HasOne<Shift>().WithMany().HasForeignKey(s => s.ShiftId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Refund>().HasOne<Shift>().WithMany().HasForeignKey(s => s.ShiftId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CustomerDuePayment>().HasOne<Shift>().WithMany().HasForeignKey(s => s.ShiftId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ReceiptSequence>(entity => entity.Property(e => e.BusinessDate).HasColumnType("date"));

        modelBuilder.Entity<CustomerDuePayment>(entity =>
        {
            entity.Property(e => e.ReceivedAt).HasColumnType("datetime2");
            entity.HasOne(e => e.Customer).WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.CustomerId, e.ReceivedAt });
        });

        modelBuilder.Entity<Customer>(entity =>
            entity.ToTable(t => t.HasCheckConstraint("CK_Customers_DueBalance", "[DueBalance] >= 0")));

        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.Property(e => e.OccurredAt).HasColumnType("datetime2");
            entity.HasIndex(e => e.OccurredAt);
            entity.HasIndex(e => new { e.EntityType, e.EntityKey });
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
