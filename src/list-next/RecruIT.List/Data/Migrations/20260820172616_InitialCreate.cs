using System;
using Microsoft.EntityFrameworkCore.Migrations;
using RecruIT.List.Models;

#nullable disable

namespace RecruIT.List.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .AlterDatabase()
                .Annotation("Npgsql:Enum:notification_channel", "email,in_app")
                .Annotation("Npgsql:Enum:trial_permission_level", "coordinator,trial_admin,viewer");

            migrationBuilder.CreateTable(
                name: "notification_deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_subscription_id = table.Column<Guid>(
                        type: "uuid",
                        nullable: false
                    ),
                    subject_id = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    channel = table.Column<NotificationChannel>(
                        type: "notification_channel",
                        nullable: false
                    ),
                    scheduled_for = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    sent_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    read_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_deliveries", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "notification_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trial_identifier = table.Column<string>(type: "text", nullable: false),
                    patient_reference = table.Column<string>(type: "text", nullable: false),
                    patient_display_name = table.Column<string>(type: "text", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    dedupe_key = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_events", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "notification_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trial_identifier = table.Column<string>(type: "text", nullable: false),
                    subject_id = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    frequency = table.Column<int>(type: "integer", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: true),
                    time_of_day = table.Column<TimeOnly>(
                        type: "time without time zone",
                        nullable: true
                    ),
                    time_zone_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    updated_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_subscriptions", x => x.id);
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

            migrationBuilder.CreateTable(
                name: "screening_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    research_subject_identifier = table.Column<string>(
                        type: "text",
                        nullable: false
                    ),
                    trial_identifier = table.Column<string>(type: "text", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    author_subject_id = table.Column<string>(type: "text", nullable: true),
                    author_display_name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_screening_notes", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "trial_access_grants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trial_identifier_system = table.Column<string>(type: "text", nullable: false),
                    trial_identifier_value = table.Column<string>(type: "text", nullable: false),
                    subject_id = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    level = table.Column<TrialPermissionLevel>(
                        type: "trial_permission_level",
                        nullable: false
                    ),
                    granted_by = table.Column<string>(type: "text", nullable: false),
                    granted_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    updated_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trial_access_grants", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_notification_deliveries_channel_sent_at_scheduled_for",
                table: "notification_deliveries",
                columns: new[] { "channel", "sent_at", "scheduled_for" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_notification_deliveries_notification_event_id",
                table: "notification_deliveries",
                column: "notification_event_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_notification_deliveries_notification_subscription_id",
                table: "notification_deliveries",
                column: "notification_subscription_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_notification_deliveries_subject_id_channel_read_at",
                table: "notification_deliveries",
                columns: new[] { "subject_id", "channel", "read_at" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_notification_events_dedupe_key",
                table: "notification_events",
                column: "dedupe_key",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_notification_events_trial_identifier_occurred_at",
                table: "notification_events",
                columns: new[] { "trial_identifier", "occurred_at" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_notification_subscriptions_trial_identifier_subject_id",
                table: "notification_subscriptions",
                columns: new[] { "trial_identifier", "subject_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_screening_notes_research_subject_identifier",
                table: "screening_notes",
                column: "research_subject_identifier"
            );

            migrationBuilder.CreateIndex(
                name: "ix_trial_access_grants_trial_identifier_system_trial_identifie",
                table: "trial_access_grants",
                columns: new[] { "trial_identifier_system", "trial_identifier_value", "email" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "notification_deliveries");

            migrationBuilder.DropTable(name: "notification_events");

            migrationBuilder.DropTable(name: "notification_subscriptions");

            migrationBuilder.DropTable(name: "poll_cursors");

            migrationBuilder.DropTable(name: "screening_notes");

            migrationBuilder.DropTable(name: "trial_access_grants");
        }
    }
}
