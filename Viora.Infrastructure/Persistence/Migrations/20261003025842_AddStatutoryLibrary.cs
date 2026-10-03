using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStatutoryLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IssuingAuthorities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssuingAuthorities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LegalFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalFields", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StatutoryDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<short>(type: "smallint", nullable: false),
                    AuthorityId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                    PartiallyExpired = table.Column<bool>(type: "boolean", nullable: false),
                    Summary = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    SourceUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    FileUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatutoryDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatutoryDocuments_IssuingAuthorities_AuthorityId",
                        column: x => x.AuthorityId,
                        principalTable: "IssuingAuthorities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LegalSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<short>(type: "smallint", nullable: false),
                    Number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LegalSections_LegalSections_ParentId",
                        column: x => x.ParentId,
                        principalTable: "LegalSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LegalSections_StatutoryDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "StatutoryDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StatutoryDocumentFields",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatutoryDocumentFields", x => new { x.DocumentId, x.FieldId });
                    table.ForeignKey(
                        name: "FK_StatutoryDocumentFields_LegalFields_FieldId",
                        column: x => x.FieldId,
                        principalTable: "LegalFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StatutoryDocumentFields_StatutoryDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "StatutoryDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StatutoryDocumentRelations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelatedDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatutoryDocumentRelations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatutoryDocumentRelations_StatutoryDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "StatutoryDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StatutoryDocumentRelations_StatutoryDocuments_RelatedDocume~",
                        column: x => x.RelatedDocumentId,
                        principalTable: "StatutoryDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LegalSectionRelations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelatedSectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalSectionRelations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LegalSectionRelations_LegalSections_RelatedSectionId",
                        column: x => x.RelatedSectionId,
                        principalTable: "LegalSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LegalSectionRelations_LegalSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "LegalSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LegalSectionVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    ChangeType = table.Column<short>(type: "smallint", nullable: false),
                    ChangedByDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalSectionVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LegalSectionVersions_LegalSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "LegalSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LegalSectionVersions_StatutoryDocuments_ChangedByDocumentId",
                        column: x => x.ChangedByDocumentId,
                        principalTable: "StatutoryDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IssuingAuthorities_Name",
                table: "IssuingAuthorities",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegalFields_Name",
                table: "LegalFields",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegalSectionRelations_RelatedSectionId",
                table: "LegalSectionRelations",
                column: "RelatedSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_LegalSectionRelations_SectionId_RelatedSectionId_Type",
                table: "LegalSectionRelations",
                columns: new[] { "SectionId", "RelatedSectionId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegalSections_DocumentId_ParentId_Order",
                table: "LegalSections",
                columns: new[] { "DocumentId", "ParentId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_LegalSections_ParentId",
                table: "LegalSections",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_LegalSections_Path",
                table: "LegalSections",
                column: "Path");

            migrationBuilder.CreateIndex(
                name: "IX_LegalSectionVersions_ChangedByDocumentId",
                table: "LegalSectionVersions",
                column: "ChangedByDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_LegalSectionVersions_SectionId_IsPublished_ValidFrom_ValidTo",
                table: "LegalSectionVersions",
                columns: new[] { "SectionId", "IsPublished", "ValidFrom", "ValidTo" });

            migrationBuilder.CreateIndex(
                name: "IX_LegalSectionVersions_SectionId_ValidFrom",
                table: "LegalSectionVersions",
                columns: new[] { "SectionId", "ValidFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegalSectionVersions_SectionId_VersionNumber",
                table: "LegalSectionVersions",
                columns: new[] { "SectionId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryDocumentFields_FieldId",
                table: "StatutoryDocumentFields",
                column: "FieldId");

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryDocumentRelations_DocumentId_RelatedDocumentId_Type",
                table: "StatutoryDocumentRelations",
                columns: new[] { "DocumentId", "RelatedDocumentId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryDocumentRelations_RelatedDocumentId",
                table: "StatutoryDocumentRelations",
                column: "RelatedDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryDocuments_AuthorityId",
                table: "StatutoryDocuments",
                column: "AuthorityId");

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryDocuments_EffectiveFrom",
                table: "StatutoryDocuments",
                column: "EffectiveFrom");

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryDocuments_ExpiresOn",
                table: "StatutoryDocuments",
                column: "ExpiresOn");

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryDocuments_IsPublished_IssuedOn",
                table: "StatutoryDocuments",
                columns: new[] { "IsPublished", "IssuedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryDocuments_Number_AuthorityId",
                table: "StatutoryDocuments",
                columns: new[] { "Number", "AuthorityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryDocuments_Type_IssuedOn",
                table: "StatutoryDocuments",
                columns: new[] { "Type", "IssuedOn" });
            migrationBuilder.Sql("CREATE INDEX \"IX_StatutoryDocuments_Search\" ON \"StatutoryDocuments\" USING GIN (to_tsvector('simple', \"Title\" || ' ' || \"Number\"));");
            migrationBuilder.Sql("CREATE INDEX \"IX_LegalSectionVersions_Search\" ON \"LegalSectionVersions\" USING GIN (to_tsvector('simple', \"Content\"));");
            migrationBuilder.Sql("ALTER TABLE \"StatutoryDocuments\" ADD CONSTRAINT \"CK_StatutoryDates\" CHECK (\"EffectiveFrom\" >= \"IssuedOn\" AND (\"ExpiresOn\" IS NULL OR \"ExpiresOn\" >= \"EffectiveFrom\"));");
            migrationBuilder.Sql("ALTER TABLE \"LegalSectionVersions\" ADD CONSTRAINT \"CK_SectionVersionDates\" CHECK (\"ValidTo\" IS NULL OR \"ValidTo\" >= \"ValidFrom\");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegalSectionRelations");

            migrationBuilder.DropTable(
                name: "LegalSectionVersions");

            migrationBuilder.DropTable(
                name: "StatutoryDocumentFields");

            migrationBuilder.DropTable(
                name: "StatutoryDocumentRelations");

            migrationBuilder.DropTable(
                name: "LegalSections");

            migrationBuilder.DropTable(
                name: "LegalFields");

            migrationBuilder.DropTable(
                name: "StatutoryDocuments");

            migrationBuilder.DropTable(
                name: "IssuingAuthorities");
        }
    }
}
