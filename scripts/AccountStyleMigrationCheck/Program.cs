using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Viora.Infrastructure.Persistence;
using Viora.Infrastructure.Persistence.Migrations;

try
{
    var isLocal = args[0] == "--local";
    using var config = isLocal ? null : JsonDocument.Parse(File.ReadAllText(args[0]));
    var connectionString = isLocal
        ? "Host=127.0.0.1;Port=55439;Database=postgres;Username=postgres;SSL Mode=Disable"
        : config!.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString();
    var builder = new NpgsqlConnectionStringBuilder(connectionString) { Timeout = 8, CommandTimeout = 10 };
    await using var connection = new NpgsqlConnection(builder.ConnectionString);
    await connection.OpenAsync();
    // Read-only production audit: aggregate types and migration history, no identities.
    if (!isLocal)
    {
        await using (var command = new NpgsqlCommand(
            "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\" DESC LIMIT 1", connection))
            Console.WriteLine($"Database latest migration: {await command.ExecuteScalarAsync()}");
        await using (var command = new NpgsqlCommand(
            "SELECT \"AccountStyle\", count(*) FROM \"Users\" GROUP BY \"AccountStyle\" ORDER BY \"AccountStyle\"", connection))
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) Console.WriteLine($"Legacy style {reader.GetInt16(0)}: {reader.GetInt64(1)} users");
    }

    // All writes below target transaction-local TEMP tables that shadow real tables.
    await using var transaction = await connection.BeginTransactionAsync();
    async Task Execute(string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync();
    }
    await Execute("CREATE TEMP TABLE \"Users\" (\"Id\" uuid PRIMARY KEY, \"AccountStyle\" smallint NOT NULL) ON COMMIT DROP");
    var ids = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToArray();
    for (var style = 0; style < ids.Length; style++)
        await Execute($"INSERT INTO \"Users\" VALUES ('{ids[style]}', {style})");

    using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(builder.ConnectionString).Options);
    var generator = db.GetService<IMigrationsSqlGenerator>();
    var migration = new MigrateAccountStyleToLegalProfiles { ActiveProvider = "Npgsql.EntityFrameworkCore.PostgreSQL" };
    var up = generator.Generate(migration.UpOperations, db.Model).Select(x =>
        x.CommandText.Replace("CREATE TABLE \"UserAccountStyleMigrationBackup\"", "CREATE TEMP TABLE \"UserAccountStyleMigrationBackup\""));
    var down = generator.Generate(migration.DownOperations, db.Model).Select(x => x.CommandText);
    async Task Apply(IEnumerable<string> sql) { foreach (var statement in sql) await Execute(statement); }
    await Apply(up);
    var expected = new short[] { 0, 0, 0, 5, 4, 4 };
    for (var oldStyle = 0; oldStyle < ids.Length; oldStyle++)
    {
        await using var command = new NpgsqlCommand(
            $"SELECT \"AccountStyle\", \"CanCreateArticle\" FROM \"Users\" WHERE \"Id\" = '{ids[oldStyle]}'", connection, transaction);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync() || reader.GetInt16(0) != expected[oldStyle] || reader.GetBoolean(1) != (oldStyle != 0))
            throw new InvalidOperationException("Incorrect migration mapping or article grant");
    }
    Console.WriteLine("Temporary tables: all six mappings and preserved article grants passed.");

    async Task ExpectRejected(string change, IEnumerable<string> operation)
    {
        await transaction.SaveAsync("test_guard");
        await Execute(change);
        var rejected = false;
        try { await Apply(operation); }
        catch (PostgresException exception) when (exception.SqlState == "P0001") { rejected = true; }
        await transaction.RollbackAsync("test_guard");
        if (!rejected) throw new InvalidOperationException("Migration guard did not reject ambiguous data");
    }
    await ExpectRejected($"UPDATE \"Users\" SET \"CanCreateArticle\" = false WHERE \"Id\" = '{ids[1]}'", down);
    await ExpectRejected($"UPDATE \"Users\" SET \"AccountStyle\" = 1 WHERE \"Id\" = '{ids[1]}'", down);
    await ExpectRejected($"INSERT INTO \"Users\" VALUES ('{Guid.NewGuid()}', 1, false)", down);
    await Execute($"INSERT INTO \"Users\" (\"Id\", \"AccountStyle\") VALUES ('{Guid.NewGuid()}', 0)");
    await Apply(down);
    for (var oldStyle = 0; oldStyle < ids.Length; oldStyle++)
    {
        await using var command = new NpgsqlCommand(
            $"SELECT \"AccountStyle\" FROM \"Users\" WHERE \"Id\" = '{ids[oldStyle]}'", connection, transaction);
        if (Convert.ToInt16(await command.ExecuteScalarAsync()) != oldStyle)
            throw new InvalidOperationException("Rollback did not restore exact original style");
    }
    await ExpectRejected($"INSERT INTO \"Users\" VALUES ('{Guid.NewGuid()}', 99)", up);
    Console.WriteLine("Temporary tables: exact rollback, new Personal default, changed-grant/type guards and unknown-style guard passed.");
    await transaction.RollbackAsync();
    Console.WriteLine("No persistent schema, data or migration history changes applied.");
}
catch (Exception exception)
{
    // Do not expose connection strings, server addresses or credentials.
    Console.WriteLine($"Audit could not complete: {exception.GetType().Name}; inner: {exception.InnerException?.GetType().Name ?? "none"}");
    Environment.ExitCode = 1;
}
