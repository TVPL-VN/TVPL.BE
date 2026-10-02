using System.Data;
using System.Text.Json;
using Npgsql;

try
{
    if (args.Length < 2) throw new ArgumentException("Specify configuration file and table-name prefixes.");
    using var config = JsonDocument.Parse(File.ReadAllText(args[0]));
    var settings = new NpgsqlConnectionStringBuilder(config.RootElement.GetProperty("ConnectionStrings")
        .GetProperty("DefaultConnection").GetString()) { Timeout = 8, CommandTimeout = 30 };
    await using var connection = new NpgsqlConnection(settings.ConnectionString);
    await connection.OpenAsync();
    await using var tx = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead);
    await using (var command = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, tx)) await command.ExecuteNonQueryAsync();
    var tables = new List<(string Schema, string Name, uint Oid)>();
    await using (var command = new NpgsqlCommand("""
        SELECT n.nspname, c.relname, c.oid FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE c.relkind IN ('r', 'p') AND n.nspname NOT IN ('pg_catalog', 'information_schema')
        AND EXISTS (SELECT 1 FROM unnest(@prefixes::text[]) prefix WHERE starts_with(lower(c.relname), lower(prefix)))
        ORDER BY n.nspname, c.relname
        """, connection, tx))
    {
        command.Parameters.AddWithValue("prefixes", args.Skip(1).ToArray());
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) tables.Add((reader.GetString(0), reader.GetString(1), reader.GetFieldValue<uint>(2)));
    }
    static string Quote(string value) => '"' + value.Replace("\"", "\"\"") + '"';
    foreach (var table in tables)
    {
        await using var count = new NpgsqlCommand($"SELECT count(*) FROM {Quote(table.Schema)}.{Quote(table.Name)}", connection, tx);
        Console.WriteLine($"TABLE {table.Schema}.{table.Name}: {await count.ExecuteScalarAsync()} rows");
        await using var command = new NpgsqlCommand("""
            SELECT 'CONSTRAINT', conname, pg_get_constraintdef(oid)
            FROM pg_constraint WHERE conrelid = @oid
            UNION ALL SELECT 'INDEX', indexrelid::regclass::text, pg_get_indexdef(indexrelid)
            FROM pg_index WHERE indrelid = @oid
            UNION ALL SELECT 'INBOUND FK', conrelid::regclass::text || '.' || conname, pg_get_constraintdef(oid)
            FROM pg_constraint WHERE confrelid = @oid AND contype = 'f'
            ORDER BY 1, 2
            """, connection, tx);
        command.Parameters.AddWithValue("oid", NpgsqlTypes.NpgsqlDbType.Oid, table.Oid);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) Console.WriteLine($"  {reader.GetString(0)} {reader.GetString(1)}: {reader.GetString(2)}");
    }
    await tx.RollbackAsync();
    Console.WriteLine($"Read-only schema inspection complete: {tables.Count} tables; no rows exported or changed.");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Schema inspection failed ({ex.GetType().Name}); connection details omitted.");
    Environment.ExitCode = 1;
}
