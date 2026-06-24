using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pinowo.Migrations
{
    /// <inheritdoc />
    public partial class AddSettlementChainFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SettlementTokenAmount",
                table: "ExpenseShares",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementTxHash",
                table: "ExpenseShares",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SettlementTokenAmount",
                table: "ExpenseShares");

            migrationBuilder.DropColumn(
                name: "SettlementTxHash",
                table: "ExpenseShares");
        }
    }
}
