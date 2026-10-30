using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnktAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_EmailOrPhone",
                table: "Accounts");

            migrationBuilder.AddColumn<string>(
                name: "IdentityProvider",
                table: "Accounts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnktIdentities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    MiniAppId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Environment = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AnktUserId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ProfileJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnktIdentities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnktIdentities_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnktGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdentityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Revoked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnktGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnktGrants_AnktIdentities_IdentityId",
                        column: x => x.IdentityId,
                        principalTable: "AnktIdentities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_EmailOrPhone",
                table: "Accounts",
                sql: "\"Email\" IS NOT NULL OR \"Phone\" IS NOT NULL OR COALESCE(\"IdentityProvider\", '') = 'ankt'");

            migrationBuilder.CreateIndex(
                name: "IX_AnktGrants_ExpiresAt",
                table: "AnktGrants",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_AnktGrants_IdentityId",
                table: "AnktGrants",
                column: "IdentityId");

            migrationBuilder.CreateIndex(
                name: "IX_AnktIdentities_AccountId",
                table: "AnktIdentities",
                column: "AccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnktIdentities_MiniAppId_Environment_AnktUserId",
                table: "AnktIdentities",
                columns: new[] { "MiniAppId", "Environment", "AnktUserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnktGrants");

            migrationBuilder.DropTable(
                name: "AnktIdentities");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_EmailOrPhone",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "IdentityProvider",
                table: "Accounts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_EmailOrPhone",
                table: "Accounts",
                sql: "\"Email\" IS NOT NULL OR \"Phone\" IS NOT NULL");
        }
    }
}
