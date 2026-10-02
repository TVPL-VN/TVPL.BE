using System.Data;
using Npgsql;

internal static class AccountStyleSchemaAudit
{
    public static async Task RunAsync(NpgsqlConnection connection)
    {
        await using var tx = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, tx)) await readOnly.ExecuteNonQueryAsync();
        async Task Print(string sql)
        {
            await using var command = new NpgsqlCommand(sql, connection, tx);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) Console.WriteLine(string.Join(" | ", Enumerable.Range(0, reader.FieldCount)
                .Select(i => reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString())));
        }
        Console.WriteLine("Latest migration | cleanup applied:");
        await Print("""
            SELECT (SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1),
                   EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002034347_ConsolidateAccountStyleOnUsers')
            """);
        Console.WriteLine("Users counts: total | invalid/null current styles:");
        await Print("SELECT count(*), count(*) FILTER (WHERE \"AccountStyle\" IS NULL OR \"AccountStyle\" NOT BETWEEN 0 AND 5) FROM \"Users\"");
        Console.WriteLine("Current style | users:");
        await Print("SELECT \"AccountStyle\", count(*) FROM \"Users\" GROUP BY \"AccountStyle\" ORDER BY \"AccountStyle\"");
        Console.WriteLine("Users scalar column metadata:");
        await Print("""
            SELECT table_schema, column_name, data_type, is_nullable, column_default
            FROM information_schema.columns
            WHERE table_name = 'Users' AND column_name IN ('AccountStyle', 'CanCreateArticle')
            ORDER BY table_schema, column_name
            """);
        var tables = new List<(string Schema, string Name)>();
        await using (var command = new NpgsqlCommand("""
            SELECT n.nspname, c.relname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind = 'r' AND c.relname ILIKE '%AccountStyle%'
              AND n.nspname NOT IN ('pg_catalog', 'information_schema')
            ORDER BY n.nspname, c.relname
            """, connection, tx))
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) tables.Add((reader.GetString(0), reader.GetString(1)));
        static string Quote(string value) => '"' + value.Replace("\"", "\"\"") + '"';
        foreach (var table in tables)
        {
            var qualified = $"{Quote(table.Schema)}.{Quote(table.Name)}";
            Console.WriteLine($"Table: {table.Schema}.{table.Name}{(table.Name == "UserAccountStyleMigrationBackup" ? " (legacy rollback archive, not current styles)" : "")}");
            await Print($"SELECT count(*) AS rows FROM {qualified}");
            var columns = new List<string>();
            await using (var command = new NpgsqlCommand("SELECT column_name FROM information_schema.columns WHERE table_schema = @schema AND table_name = @table ORDER BY ordinal_position", connection, tx))
            {
                command.Parameters.AddWithValue("schema", table.Schema); command.Parameters.AddWithValue("table", table.Name);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync()) columns.Add(reader.GetString(0));
            }
            Console.WriteLine("Columns: " + string.Join(", ", columns));
            if (columns.Contains("UserId") && columns.Contains("AccountStyle"))
            {
                var users = $"{Quote(table.Schema)}.\"Users\"";
                Console.WriteLine("Counts: orphan rows | equal numeric values | different numeric values | invalid styles | users with multiple distinct styles");
                await Print($"""
                    SELECT count(*) FILTER (WHERE u."Id" IS NULL),
                           count(*) FILTER (WHERE m."AccountStyle" = u."AccountStyle"),
                           count(*) FILTER (WHERE m."AccountStyle" IS DISTINCT FROM u."AccountStyle" AND u."Id" IS NOT NULL),
                           count(*) FILTER (WHERE m."AccountStyle" IS NULL OR m."AccountStyle" NOT BETWEEN 0 AND 5),
                           (SELECT count(*) FROM (SELECT "UserId" FROM {qualified} GROUP BY "UserId" HAVING count(DISTINCT "AccountStyle") > 1) x)
                    FROM {qualified} m LEFT JOIN {users} u ON u."Id" = m."UserId"
                    """);
            }
        }
        if (tables.Count == 0) Console.WriteLine("No AccountStyle auxiliary tables found.");
        await tx.RollbackAsync();
        Console.WriteLine("Read-only audit completed; no identities, data or schema changes.");
    }
}
