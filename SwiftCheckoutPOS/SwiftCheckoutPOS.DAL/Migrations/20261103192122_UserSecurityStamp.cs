using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwiftCheckoutPOS.DAL.Migrations
{
    /// <inheritdoc />
    public partial class UserSecurityStamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "Users",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValueSql: "CONVERT(nvarchar(40), NEWID())");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "Users");
        }
    }
}
