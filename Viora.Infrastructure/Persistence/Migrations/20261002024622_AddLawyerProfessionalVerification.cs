using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLawyerProfessionalVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProfessionalVerifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<short>(type: "smallint", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    CompletedStep = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VerificationData = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfessionalVerifications", x => x.Id);
                    table.CheckConstraint("CK_ProfessionalVerifications_Status", "\"Status\" BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_ProfessionalVerifications_Step", "\"CompletedStep\" BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_ProfessionalVerifications_SubmittedAt", "(\"Status\" = 0 AND \"SubmittedAt\" IS NULL) OR (\"Status\" <> 0 AND \"SubmittedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_ProfessionalVerifications_Type", "\"Type\" = 1");
                    table.ForeignKey(
                        name: "FK_ProfessionalVerifications_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VerificationFileCleanups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificationFileCleanups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VerificationDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VerificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<short>(type: "smallint", nullable: false),
                    FileKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificationDocuments", x => x.Id);
                    table.CheckConstraint("CK_VerificationDocuments_Size", "\"FileSize\" > 0 AND \"FileSize\" <= 10485760");
                    table.CheckConstraint("CK_VerificationDocuments_Type", "\"DocumentType\" IN (1, 2, 99)");
                    table.ForeignKey(
                        name: "FK_VerificationDocuments_ProfessionalVerifications_Verificatio~",
                        column: x => x.VerificationId,
                        principalTable: "ProfessionalVerifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_ProfessionalVerifications_Active",
                table: "ProfessionalVerifications",
                columns: new[] { "AccountId", "Type" },
                unique: true,
                filter: "\"Status\" IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationDocuments_VerificationId_DocumentType",
                table: "VerificationDocuments",
                columns: new[] { "VerificationId", "DocumentType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VerificationDocuments");

            migrationBuilder.DropTable(
                name: "VerificationFileCleanups");

            migrationBuilder.DropTable(
                name: "ProfessionalVerifications");
        }
    }
}
