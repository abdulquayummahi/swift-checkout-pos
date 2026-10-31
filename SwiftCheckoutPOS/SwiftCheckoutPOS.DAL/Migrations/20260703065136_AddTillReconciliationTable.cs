using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwiftCheckoutPOS.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddTillReconciliationTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TillReconciliations",
                columns: table => new
                {
                    ReconciliationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    OpenTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CloseTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpectedCash = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CountedCash = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TillReconciliations", x => x.ReconciliationId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TillReconciliations");
        }
    }
}
