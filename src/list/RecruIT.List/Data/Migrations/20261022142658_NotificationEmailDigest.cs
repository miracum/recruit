using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruIT.List.Data.Migrations
{
    /// <inheritdoc />
    public partial class NotificationEmailDigest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "email_digest_id",
                table: "notification_deliveries",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_notification_deliveries_email_digest_id",
                table: "notification_deliveries",
                column: "email_digest_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_notification_deliveries_email_digest_id",
                table: "notification_deliveries"
            );

            migrationBuilder.DropColumn(name: "email_digest_id", table: "notification_deliveries");
        }
    }
}
