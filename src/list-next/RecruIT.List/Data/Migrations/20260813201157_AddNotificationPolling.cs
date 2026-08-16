using System;
using Microsoft.EntityFrameworkCore.Migrations;
using RecruIT.List.Models;

#nullable disable

namespace RecruIT.List.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationPolling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .AlterDatabase()
                .Annotation("Npgsql:Enum:notification_channel", "email")
                .Annotation("Npgsql:Enum:trial_permission_level", "coordinator,trial_admin,viewer")
                .OldAnnotation(
                    "Npgsql:Enum:trial_permission_level",
                    "coordinator,trial_admin,viewer"
                );

            migrationBuilder.CreateTable(
                name: "notification_recipients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trial_identifier_system = table.Column<string>(type: "text", nullable: false),
                    trial_identifier_value = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    channel = table.Column<NotificationChannel>(
                        type: "notification_channel",
                        nullable: false
                    ),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_recipients", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "poll_cursors",
                columns: table => new
                {
                    list_id = table.Column<string>(type: "text", nullable: false),
                    last_seen_version_id = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_poll_cursors", x => x.list_id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_notification_recipients_trial_identifier_system_trial_ident",
                table: "notification_recipients",
                columns: new[]
                {
                    "trial_identifier_system",
                    "trial_identifier_value",
                    "email",
                    "channel",
                },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "notification_recipients");

            migrationBuilder.DropTable(name: "poll_cursors");

            migrationBuilder
                .AlterDatabase()
                .Annotation("Npgsql:Enum:trial_permission_level", "coordinator,trial_admin,viewer")
                .OldAnnotation("Npgsql:Enum:notification_channel", "email")
                .OldAnnotation(
                    "Npgsql:Enum:trial_permission_level",
                    "coordinator,trial_admin,viewer"
                );
        }
    }
}
