using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwiftCheckoutPOS.DAL.Migrations
{
    /// <inheritdoc />
    public partial class HeldReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "HeldAt",
                table: "Sales",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReservedQuantity",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_OrderStatus_HeldAt",
                table: "Sales",
                columns: new[] { "OrderStatus", "HeldAt" });

            // Data conversion (reviewed): before BP-03, holding DEDUCTED stock and resuming added it back.
            // Held orders: give the units back to owned stock and record them as reserved.
            // Old "Resumed" rows had already released their stock; mark them Cancelled (kept in history).
            migrationBuilder.Sql(@"
UPDATE p SET p.StockQuantity = p.StockQuantity + h.Qty, p.ReservedQuantity = p.ReservedQuantity + h.Qty
FROM Products p
JOIN (SELECT si.ProductId, SUM(si.Quantity) AS Qty
      FROM SaleItems si JOIN Sales s ON s.SaleId = si.SaleId
      WHERE s.OrderStatus = N'Held' GROUP BY si.ProductId) h ON h.ProductId = p.ProductId;
UPDATE Sales SET HeldAt = SaleDate WHERE OrderStatus = N'Held' AND HeldAt IS NULL;
UPDATE Sales SET OrderStatus = N'Cancelled' WHERE OrderStatus = N'Resumed';");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Reserved",
                table: "Products",
                sql: "[ReservedQuantity] >= 0 AND [ReservedQuantity] <= [StockQuantity]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sales_OrderStatus_HeldAt",
                table: "Sales");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Reserved",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "HeldAt",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "ReservedQuantity",
                table: "Products");
        }
    }
}
