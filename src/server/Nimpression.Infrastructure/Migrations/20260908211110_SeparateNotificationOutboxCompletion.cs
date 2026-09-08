using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nimpression.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeparateNotificationOutboxCompletion : Migration
    {
        private static readonly string[] NotificationCompletionColumns = ["NotificationProcessedAt", "OccurredAt"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NotificationProcessedAt",
                table: "OutboxMessages",
                type: "timestamptz",
                nullable: true);

            // Preserve legacy completion so upgrading does not replay historical notifications.
            migrationBuilder.Sql("""
                UPDATE "OutboxMessages"
                SET "NotificationProcessedAt" = "ProcessedAt"
                WHERE "ProcessedAt" IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_NotificationProcessedAt_OccurredAt",
                table: "OutboxMessages",
                columns: NotificationCompletionColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_NotificationProcessedAt_OccurredAt",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "NotificationProcessedAt",
                table: "OutboxMessages");
        }
    }
}
