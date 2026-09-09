using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nimpression.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayslipSettlementSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Settlement",
                table: "Payslips",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Settlement",
                table: "Payslips");
        }
    }
}
