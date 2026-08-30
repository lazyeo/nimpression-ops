using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nimpression.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailLogUniqueIndex : Migration
    {
        private static readonly string[] CorrelationIdAndToAddressColumns = ["CorrelationId", "ToAddress"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailLogs_CorrelationId",
                table: "EmailLogs");

            migrationBuilder.CreateIndex(
                name: "IX_EmailLogs_CorrelationId_ToAddress",
                table: "EmailLogs",
                columns: CorrelationIdAndToAddressColumns,
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailLogs_CorrelationId_ToAddress",
                table: "EmailLogs");

            migrationBuilder.CreateIndex(
                name: "IX_EmailLogs_CorrelationId",
                table: "EmailLogs",
                column: "CorrelationId");
        }
    }
}
