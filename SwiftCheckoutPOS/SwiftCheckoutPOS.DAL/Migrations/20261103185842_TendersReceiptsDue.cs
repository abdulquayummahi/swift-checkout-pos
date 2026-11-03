using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwiftCheckoutPOS.DAL.Migrations
{
    /// <inheritdoc />
    public partial class TendersReceiptsDue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                table: "Sales",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperationKey",
                table: "Sales",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "SaleItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DueBalance",
                table: "Customers",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "CustomerDuePayments",
                columns: table => new
                {
                    CustomerDuePaymentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Tender = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Actor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerDuePayments", x => x.CustomerDuePaymentId);
                    table.ForeignKey(
                        name: "FK_CustomerDuePayments_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptSequences",
                columns: table => new
                {
                    BusinessDate = table.Column<DateTime>(type: "date", nullable: false),
                    LastNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptSequences", x => x.BusinessDate);
                });

            migrationBuilder.CreateTable(
                name: "SalePayments",
                columns: table => new
                {
                    SalePaymentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SaleId = table.Column<int>(type: "int", nullable: false),
                    Tender = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Tendered = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalePayments", x => x.SalePaymentId);
                    table.CheckConstraint("CK_SalePayments_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_SalePayments_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "SaleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_OperationKey",
                table: "Sales",
                column: "OperationKey",
                unique: true,
                filter: "[OperationKey] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Customers_DueBalance",
                table: "Customers",
                sql: "[DueBalance] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerDuePayments_CustomerId_ReceivedAt",
                table: "CustomerDuePayments",
                columns: new[] { "CustomerId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalePayments_SaleId",
                table: "SalePayments",
                column: "SaleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerDuePayments");

            migrationBuilder.DropTable(
                name: "ReceiptSequences");

            migrationBuilder.DropTable(
                name: "SalePayments");

            migrationBuilder.DropIndex(
                name: "IX_Sales_OperationKey",
                table: "Sales");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Customers_DueBalance",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "OperationKey",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "DueBalance",
                table: "Customers");
        }
    }
}
