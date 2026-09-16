using System.Reflection;
using DbUp;
using Microsoft.Data.SqlClient;

var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Mashal");
if (string.IsNullOrWhiteSpace(connection))
{
    Console.Error.WriteLine("ConnectionStrings__Mashal is required. Supply it through environment configuration.");
    return 1;
}
try
{
    if (args.Contains("--preflight"))
    {
        await using var sql = new SqlConnection(connection);
        await sql.OpenAsync();
        if (Environment.GetEnvironmentVariable("MASHAL_ENVIRONMENT") == "Production")
        {
            var path = Environment.GetEnvironmentVariable("MASHAL_BACKUP_PATH");
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("Production backup path is required.");
            var file = path.TrimEnd((char)92, '/') + "/Mashal-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".bak";
            var database = sql.Database.Replace("]", "]]");
            await using var backup = new SqlCommand($"BACKUP DATABASE [{database}] TO DISK=@path WITH COPY_ONLY,CHECKSUM; RESTORE VERIFYONLY FROM DISK=@path WITH CHECKSUM;", sql);
            backup.Parameters.AddWithValue("@path", file);
            backup.CommandTimeout = 600;
            await backup.ExecuteNonQueryAsync();
            Console.WriteLine("Production backup created and verified.");
        }
        Console.WriteLine("Database preflight passed.");
        return 0;
    }
    var engine = DeployChanges.To.SqlDatabase(connection)
        .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
        .JournalToSqlTable("dbo", "SchemaVersions")
        .WithTransactionPerScript()
        .LogToConsole().Build();
    if (!engine.TryConnect(out _)) { Console.Error.WriteLine("Database connection failed."); return 1; }
    var pending = engine.GetScriptsToExecute();
    Console.WriteLine($"Pending migrations: {pending.Count}");
    if (args.Contains("--verify-no-pending")) return pending.Count == 0 ? 0 : 1;
    var result = engine.PerformUpgrade();
    if (!result.Successful) { Console.Error.WriteLine("Migration failed. Deployment must stop."); return 1; }
    return 0;
}
catch { Console.Error.WriteLine("Migration runner failed. Check secure database diagnostics."); return 1; }
