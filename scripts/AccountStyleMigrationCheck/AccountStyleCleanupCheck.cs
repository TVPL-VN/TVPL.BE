using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Viora.Infrastructure.Persistence;
using Viora.Infrastructure.Persistence.Migrations;

internal static class AccountStyleCleanupCheck
{
    public static async Task RunAsync(NpgsqlConnection connection)
    {
        await using var tx = await connection.BeginTransactionAsync();
        async Task Execute(string sql)
        {
            await using var command = new NpgsqlCommand(sql, connection, tx);
            await command.ExecuteNonQueryAsync();
        }
        async Task<object?> Scalar(string sql)
        {
            await using var command = new NpgsqlCommand(sql, connection, tx);
            return await command.ExecuteScalarAsync();
        }
        void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection.ConnectionString).Options);
        var generator = db.GetService<IMigrationsSqlGenerator>();
        var migration = new ConsolidateAccountStyleOnUsers { ActiveProvider = "Npgsql.EntityFrameworkCore.PostgreSQL" };
        async Task Apply() { foreach (var sql in generator.Generate(migration.UpOperations, db.Model)) await Execute(sql.CommandText); }
        async Task Rejected(string sqlState)
        {
            await Execute("SAVEPOINT rejected");
            try { await Apply(); throw new InvalidOperationException("Unsafe cleanup accepted."); }
            catch (PostgresException ex) when (ex.SqlState == sqlState) { await Execute("ROLLBACK TO SAVEPOINT rejected"); }
        }
        await Execute("CREATE TEMP TABLE \"Users\" (\"Id\" uuid PRIMARY KEY, \"AccountStyle\" smallint NOT NULL, \"CanCreateArticle\" boolean NOT NULL)");
        await Execute("CREATE TEMP TABLE \"UserAccountStyleMigrationBackup\" (\"UserId\" uuid PRIMARY KEY, \"AccountStyle\" smallint NOT NULL)");
        var id = Guid.Parse("10000000-0000-0000-0000-000000000000");
        for (var i = 0; i < 6; i++)
            await Execute($"INSERT INTO \"Users\" VALUES ('10000000-0000-0000-0000-{i:D12}', {i}, {(i % 2 == 0 ? "true" : "false")})");
        await Execute($"INSERT INTO \"UserAccountStyleMigrationBackup\" VALUES ('{id:D}', 5)");
        var legacyMappings = new[] { "UserAccountStyleMappings", "UserAccountStyles", "AccountStyleMapping" };
        foreach (var table in legacyMappings)
        {
            await Execute($"CREATE TEMP TABLE \"{table}\" (\"UserId\" uuid REFERENCES \"Users\"(\"Id\"), \"AccountStyle\" smallint)");
            await Execute($"INSERT INTO \"{table}\" VALUES ('{id:D}', 5)");
        }
        var before = await Scalar("SELECT jsonb_agg(u ORDER BY \"Id\")::text FROM \"Users\" u");
        await Execute("CREATE TEMP TABLE \"RetainedDependency\" (\"UserId\" uuid REFERENCES \"UserAccountStyleMigrationBackup\"(\"UserId\"))");
        await Rejected("2BP01");
        Require((long)(await Scalar("SELECT count(*) FROM \"UserAccountStyleMigrationBackup\""))! == 1, "Rejected cleanup lost backup.");
        Require(before!.Equals(await Scalar("SELECT jsonb_agg(u ORDER BY \"Id\")::text FROM \"Users\" u")), "Rejected cleanup changed users.");
        await Execute("DROP TABLE \"RetainedDependency\"");
        await Execute("UPDATE \"Users\" SET \"AccountStyle\" = 6 WHERE \"AccountStyle\" = 5");
        await Rejected("P0001");
        await Execute("UPDATE \"Users\" SET \"AccountStyle\" = 5 WHERE \"AccountStyle\" = 6");
        await Execute("ALTER TABLE \"Users\" RENAME COLUMN \"AccountStyle\" TO \"RetainedStyle\"");
        await Rejected("P0001");
        await Execute("ALTER TABLE \"Users\" RENAME COLUMN \"RetainedStyle\" TO \"AccountStyle\"");
        await Execute("ALTER TABLE \"Users\" RENAME TO \"RetainedUsers\"");
        await Rejected("P0001");
        await Execute("ALTER TABLE \"RetainedUsers\" RENAME TO \"Users\"");
        await Apply();
        Require(before!.Equals(await Scalar("SELECT jsonb_agg(u ORDER BY \"Id\")::text FROM \"Users\" u")), "Current user data changed.");
        Require((bool)(await Scalar("SELECT to_regclass('\"UserAccountStyleMigrationBackup\"') IS NULL"))!, "Backup not removed.");
        foreach (var table in legacyMappings)
            Require((bool)(await Scalar($"SELECT to_regclass('\"{table}\"') IS NULL"))!, "Legacy mapping not removed.");
        await Execute("SAVEPOINT invalid_style");
        try { await Execute("UPDATE \"Users\" SET \"AccountStyle\" = 6"); throw new InvalidOperationException("Invalid style accepted."); }
        catch (PostgresException ex) when (ex.SqlState == "23514") { await Execute("ROLLBACK TO SAVEPOINT invalid_style"); }
        await Execute("SAVEPOINT rollback_guard");
        try
        {
            foreach (var sql in generator.Generate(migration.DownOperations, db.Model)) await Execute(sql.CommandText);
            throw new InvalidOperationException("Rollback accepted without archived data restoration.");
        }
        catch (PostgresException ex) when (ex.SqlState == "P0001") { await Execute("ROLLBACK TO SAVEPOINT rollback_guard"); }
        await Execute("ALTER TABLE \"Users\" DROP CONSTRAINT \"CK_Users_AccountStyle\"");
        await Execute("CREATE TEMP TABLE \"UserAccountStyleMigrationBackup\" (\"UserId\" uuid PRIMARY KEY, \"AccountStyle\" smallint NOT NULL)");
        await Apply(); // Empty fresh-install backup also works.
        await Execute("ALTER TABLE \"Users\" DROP CONSTRAINT \"CK_Users_AccountStyle\"");
        await Apply(); // Already absent backup is supported.
        await tx.RollbackAsync();
        Console.WriteLine("Cleanup checks passed without settings: populated backup removed, users/styles/grants preserved, external FK/invalid current values/missing Users or column rejected, rollback guarded, empty/absent backup supported. No persistent changes.");
    }
}
