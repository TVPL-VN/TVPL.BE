using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStatutorySectionPaths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LegalSections_Path",
                table: "LegalSections");

            migrationBuilder.CreateIndex(
                name: "IX_LegalSections_DocumentId_Path",
                table: "LegalSections",
                columns: new[] { "DocumentId", "Path" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LegalSections_DocumentId_Path",
                table: "LegalSections");

            migrationBuilder.CreateIndex(
                name: "IX_LegalSections_Path",
                table: "LegalSections",
                column: "Path");
        }
    }
}
