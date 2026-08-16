using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace list.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScreeningNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateIndex(
                name: "ix_screening_notes_research_subject_identifier",
                table: "screening_notes",
                column: "research_subject_identifier"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "screening_notes");
        }
    }
}
