using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwiftCheckoutPOS.DAL.Migrations
{
    /// <inheritdoc />
    public partial class CashRoundingAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RoundingAmount",
                table: "Sales",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RoundingAmount",
                table: "Sales");
        }
    }
}
