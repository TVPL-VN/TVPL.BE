using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Viora.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261014100000_AdminControlsArticlePublishing")]
public sealed class AdminControlsArticlePublishing : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("UPDATE \"Users\" SET \"CanCreateArticle\" = \"AccountStyle\" IN (1, 2, 3)");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("UPDATE \"Users\" SET \"CanCreateArticle\" = \"AccountStyle\" IN (1, 2, 3, 4, 5)");
}
