using System.Reflection;
using DbUp;

var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Mashal");
if (string.IsNullOrWhiteSpace(connection))
{
    Console.Error.WriteLine("ConnectionStrings__Mashal is required. Supply it through environment configuration.");
    return 1;
}
try
{
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
