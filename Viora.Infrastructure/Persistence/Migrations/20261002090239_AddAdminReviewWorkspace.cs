using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminReviewWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "ProfessionalVerifications",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "ProfessionalVerifications",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "ProfessionalVerifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewerUserId",
                table: "ProfessionalVerifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SessionVersion",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalVerifications_ReviewerUserId",
                table: "ProfessionalVerifications",
                column: "ReviewerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProfessionalVerifications_Users_ReviewerUserId",
                table: "ProfessionalVerifications",
                column: "ReviewerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProfessionalVerifications_Users_ReviewerUserId",
                table: "ProfessionalVerifications");

            migrationBuilder.DropIndex(
                name: "IX_ProfessionalVerifications_ReviewerUserId",
                table: "ProfessionalVerifications");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "ProfessionalVerifications");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "ProfessionalVerifications");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "ProfessionalVerifications");

            migrationBuilder.DropColumn(
                name: "ReviewerUserId",
                table: "ProfessionalVerifications");

            migrationBuilder.DropColumn(
                name: "SessionVersion",
                table: "Accounts");
        }
    }
}
