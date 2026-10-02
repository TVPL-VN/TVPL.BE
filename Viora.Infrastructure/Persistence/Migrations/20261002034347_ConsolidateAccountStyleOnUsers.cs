using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateAccountStyleOnUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Historical rollback data uses the old taxonomy. Never copy it into Users.
            migrationBuilder.Sql("""
                DO $cleanup$
                DECLARE
                    legacy_table text;
                BEGIN
                    IF to_regclass('"Users"') IS NULL THEN
                        RAISE EXCEPTION 'Users table must exist before cleanup';
                    END IF;
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_attribute WHERE attrelid = to_regclass('"Users"')
                        AND attname = 'AccountStyle' AND NOT attisdropped
                    ) THEN
                        RAISE EXCEPTION 'Users.AccountStyle must exist before cleanup';
                    END IF;
                    LOCK TABLE "Users" IN ACCESS EXCLUSIVE MODE;
                    IF EXISTS (SELECT 1 FROM "Users" WHERE "AccountStyle" IS NULL OR "AccountStyle" NOT BETWEEN 0 AND 5) THEN
                        RAISE EXCEPTION 'Invalid current Users.AccountStyle; inspect values before cleanup';
                    END IF;
                    -- Explicit legacy names only. Never derive current profile values from them.
                    FOREACH legacy_table IN ARRAY ARRAY[
                        'UserAccountStyleMappings', 'UserAccountStyles', 'AccountStyleMapping',
                        'UserAccountStyleMigrationBackup'
                    ] LOOP
                        IF to_regclass(format('%I', legacy_table)) IS NOT NULL THEN
                            -- RESTRICT is the default: unexpected dependencies abort the transaction.
                            EXECUTE format('DROP TABLE %I', legacy_table);
                        END IF;
                    END LOOP;
                END
                $cleanup$;
                """);
            migrationBuilder.AlterColumn<short>(
                name: "AccountStyle",
                table: "Users",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0,
                oldClrType: typeof(short),
                oldType: "smallint");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_AccountStyle",
                table: "Users",
                sql: "\"AccountStyle\" BETWEEN 0 AND 5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $rollback$
                BEGIN
                    IF to_regclass('"UserAccountStyleMigrationBackup"') IS NULL THEN
                        RAISE EXCEPTION 'Restore the original legacy backup before rolling back AccountStyle cleanup';
                    END IF;
                END
                $rollback$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_AccountStyle",
                table: "Users");

            migrationBuilder.AlterColumn<short>(
                name: "AccountStyle",
                table: "Users",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldDefaultValue: (short)0);
        }
    }
}
