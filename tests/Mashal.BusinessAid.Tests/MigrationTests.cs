using Dapper;
using DbUp;
using DbUp.Engine;
using Mashal.BusinessAid.Api.Data;
using Xunit;
namespace Mashal.Tests;

[Trait("Category", "Integration")]
public class MigrationTests
{
    private readonly string connection = Environment.GetEnvironmentVariable("ConnectionStrings__Mashal") ?? throw new InvalidOperationException("A dedicated test database is required.");
    [Fact]
    public async Task FailedScriptRollsBackAndIsNotJournaled()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var table = "RollbackTest" + suffix;
        var journal = "JournalTest" + suffix;
        var script = new SqlScript("001_failure.sql", $"CREATE TABLE dbo.{table}(Id int NOT NULL); THROW 51000, 'Intentional test failure', 1;");
        var runner = DeployChanges.To.SqlDatabase(connection).WithScripts(script).JournalToSqlTable("dbo", journal).WithTransactionPerScript().Build();
        Assert.False(runner.PerformUpgrade().Successful);
        await using var sql = await new SqlConnectionFactory(connection).Open();
        Assert.Null(await sql.ExecuteScalarAsync<int?>("SELECT OBJECT_ID(@name)", new { name = "dbo." + table }));
        var retry = DeployChanges.To.SqlDatabase(connection).WithScripts(script).JournalToSqlTable("dbo", journal).WithTransactionPerScript().Build();
        Assert.Single(retry.GetScriptsToExecute());
    }
    [Fact]
    public async Task ForwardUpgradeRunsOnceAndSkipsAppliedScripts()
    {
        var suffix = Guid.NewGuid().ToString("N"); var table = "UpgradeTest" + suffix; var journal = "JournalTest" + suffix;
        var first = new SqlScript("001_initial.sql", $"CREATE TABLE dbo.{table}(Id int NOT NULL PRIMARY KEY); INSERT dbo.{table} VALUES(1);");
        var second = new SqlScript("002_expand.sql", $"ALTER TABLE dbo.{table} ADD Description nvarchar(40) NULL;");
        var initial = DeployChanges.To.SqlDatabase(connection).WithScripts(first).JournalToSqlTable("dbo", journal).WithTransactionPerScript().Build();
        Assert.True(initial.PerformUpgrade().Successful);
        var upgrade = DeployChanges.To.SqlDatabase(connection).WithScripts(first, second).JournalToSqlTable("dbo", journal).WithTransactionPerScript().Build();
        Assert.Single(upgrade.GetScriptsToExecute());
        Assert.True(upgrade.PerformUpgrade().Successful);
        Assert.Empty(upgrade.GetScriptsToExecute());
        Assert.True(upgrade.PerformUpgrade().Successful);
        await using var sql = await new SqlConnectionFactory(connection).Open();
        Assert.Equal(1, await sql.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM dbo.{table}"));
    }
}
