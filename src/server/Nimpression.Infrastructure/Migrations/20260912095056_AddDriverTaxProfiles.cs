using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nimpression.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverTaxProfiles : Migration
    {
        private static readonly string[] SubmittedOrderColumns = ["SubmittedAt", "Id"];
        private static readonly string[] ApprovedDateColumns = ["DriverId", "EffectiveFrom"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DriverTaxProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Declaration = table.Column<string>(type: "jsonb", nullable: false),
                    ApprovedEmployee = table.Column<string>(type: "jsonb", nullable: true),
                    ApprovedContractor = table.Column<string>(type: "jsonb", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverTaxProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DriverTaxProfiles_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DriverTaxProfiles_Users_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DriverTaxProfiles_Users_SubmittedBy",
                        column: x => x.SubmittedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DriverTaxProfiles_ReviewedBy",
                table: "DriverTaxProfiles",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DriverTaxProfiles_SubmittedAt_Id",
                table: "DriverTaxProfiles",
                columns: SubmittedOrderColumns);

            migrationBuilder.CreateIndex(
                name: "IX_DriverTaxProfiles_SubmittedBy",
                table: "DriverTaxProfiles",
                column: "SubmittedBy");

            migrationBuilder.CreateIndex(
                name: "UX_DriverTaxProfiles_ApprovedDate",
                table: "DriverTaxProfiles",
                columns: ApprovedDateColumns,
                unique: true,
                filter: "\"Status\" = 'Approved'");

            migrationBuilder.CreateIndex(
                name: "UX_DriverTaxProfiles_Pending",
                table: "DriverTaxProfiles",
                column: "DriverId",
                unique: true,
                filter: "\"Status\" = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DriverTaxProfiles");
        }
    }
}
