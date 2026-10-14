using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Viora.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261014090000_AddNotificationPreferences")]
public sealed partial class AddNotificationPreferences : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<int>(
            name: "DisabledNotificationKinds", table: "UserSettings",
            type: "integer", nullable: false, defaultValue: 0);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "DisabledNotificationKinds", table: "UserSettings");
}
