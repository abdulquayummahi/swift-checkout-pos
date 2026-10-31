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
            entity.Property(e => e.OrderStatus).HasMaxLength(20);
            entity.Property(e => e.CashierName).HasMaxLength(100);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => new { e.SaleDate, e.OrderStatus });
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

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
