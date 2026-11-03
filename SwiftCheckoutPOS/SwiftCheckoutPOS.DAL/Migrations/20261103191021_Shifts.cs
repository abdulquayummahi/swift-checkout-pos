using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwiftCheckoutPOS.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Shifts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ShiftId",
                table: "Sales",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShiftId",
                table: "Refunds",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShiftId",
                table: "CustomerDuePayments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Shifts",
                columns: table => new
                {
                    ShiftId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OpenedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OpeningFloat = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CountedCash = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ExpectedCash = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Difference = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DifferenceApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shifts", x => x.ShiftId);
                });

            migrationBuilder.CreateTable(
                name: "CashMovements",
                columns: table => new
                {
                    CashMovementId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShiftId = table.Column<int>(type: "int", nullable: false),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashMovements", x => x.CashMovementId);
                    table.ForeignKey(
                        name: "FK_CashMovements_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "ShiftId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_ShiftId",
                table: "Sales",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_ShiftId",
                table: "Refunds",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerDuePayments_ShiftId",
                table: "CustomerDuePayments",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_ShiftId",
                table: "CashMovements",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "UX_Shifts_OneOpen",
                table: "Shifts",
                column: "Status",
                unique: true,
                filter: "[Status] = N'Open'");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerDuePayments_Shifts_ShiftId",
                table: "CustomerDuePayments",
                column: "ShiftId",
                principalTable: "Shifts",
                principalColumn: "ShiftId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Refunds_Shifts_ShiftId",
                table: "Refunds",
                column: "ShiftId",
                principalTable: "Shifts",
                principalColumn: "ShiftId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Shifts_ShiftId",
                table: "Sales",
                column: "ShiftId",
                principalTable: "Shifts",
                principalColumn: "ShiftId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerDuePayments_Shifts_ShiftId",
                table: "CustomerDuePayments");

            migrationBuilder.DropForeignKey(
                name: "FK_Refunds_Shifts_ShiftId",
                table: "Refunds");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Shifts_ShiftId",
                table: "Sales");

            migrationBuilder.DropTable(
                name: "CashMovements");

            migrationBuilder.DropTable(
                name: "Shifts");

            migrationBuilder.DropIndex(
                name: "IX_Sales_ShiftId",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Refunds_ShiftId",
                table: "Refunds");

            migrationBuilder.DropIndex(
                name: "IX_CustomerDuePayments_ShiftId",
                table: "CustomerDuePayments");

            migrationBuilder.DropColumn(
                name: "ShiftId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "ShiftId",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "ShiftId",
                table: "CustomerDuePayments");
        }
    }
}
