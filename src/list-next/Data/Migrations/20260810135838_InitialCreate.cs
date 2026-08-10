using System;
using Microsoft.EntityFrameworkCore.Migrations;
using list.Models;

#nullable disable

namespace list.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:trial_permission_level", "coordinator,trial_admin,viewer");

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
                    level = table.Column<TrialPermissionLevel>(type: "trial_permission_level", nullable: false),
                    granted_by = table.Column<string>(type: "text", nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trial_access_grants", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_trial_access_grants_trial_identifier_system_trial_identifie",
                table: "trial_access_grants",
                columns: ["trial_identifier_system", "trial_identifier_value", "email"],
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trial_access_grants");
        }
    }
}
