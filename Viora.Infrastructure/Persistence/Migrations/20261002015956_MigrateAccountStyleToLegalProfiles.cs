using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MigrateAccountStyleToLegalProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Prevent concurrent writes while capturing and converting the old taxonomy.
            migrationBuilder.Sql("""
                LOCK TABLE "Users" IN ACCESS EXCLUSIVE MODE;
                DO $migration$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Users" WHERE "AccountStyle" NOT BETWEEN 0 AND 5) THEN
                        RAISE EXCEPTION 'Unknown legacy AccountStyle; review data before migration.';
                    END IF;
                END $migration$;
                CREATE TABLE "UserAccountStyleMigrationBackup" (
                    "UserId" uuid PRIMARY KEY,
                    "AccountStyle" smallint NOT NULL
                );
                INSERT INTO "UserAccountStyleMigrationBackup" ("UserId", "AccountStyle")
                SELECT "Id", "AccountStyle" FROM "Users";
                """);

            migrationBuilder.AddColumn<bool>(
                name: "CanCreateArticle",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Preserve the old article grant independently BEFORE interpreting the new enum.
            // Agency was labelled "Cơ quan", with no advertising-specific rules.
            migrationBuilder.Sql("""
                UPDATE "Users"
                SET "CanCreateArticle" = "AccountStyle" IN (1, 2, 3, 4, 5),
                    "AccountStyle" = CASE "AccountStyle"
                        WHEN 3 THEN 5
                        WHEN 4 THEN 4
                        WHEN 5 THEN 4
                        ELSE 0
                    END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reject ambiguous rollback rather than inventing social types or losing grants.
            migrationBuilder.Sql("""
                LOCK TABLE "Users" IN ACCESS EXCLUSIVE MODE;
                DO $migration$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "Users" u
                        LEFT JOIN "UserAccountStyleMigrationBackup" b ON b."UserId" = u."Id"
                        WHERE (b."UserId" IS NULL AND
                            (u."AccountStyle" <> 0 OR u."CanCreateArticle"))
                        OR (b."UserId" IS NOT NULL AND (
                            u."AccountStyle" <> CASE b."AccountStyle"
                                WHEN 3 THEN 5 WHEN 4 THEN 4 WHEN 5 THEN 4 ELSE 0 END
                            OR u."CanCreateArticle" <> (b."AccountStyle" IN (1, 2, 3, 4, 5))))
                    ) THEN
                        RAISE EXCEPTION 'Profile types or article grants changed after migration; rollback requires a reviewed data reconciliation.';
                    END IF;
                END $migration$;
                UPDATE "Users" u SET "AccountStyle" = b."AccountStyle"
                FROM "UserAccountStyleMigrationBackup" b WHERE b."UserId" = u."Id";
                DROP TABLE "UserAccountStyleMigrationBackup";
                """);

            migrationBuilder.DropColumn(
                name: "CanCreateArticle",
                table: "Users");
        }
    }
}
