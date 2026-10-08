using Dapper;
using DbUp;
using DbUp.Engine;
using Ruach.BusinessAid.Api.Data;
using Microsoft.Data.SqlClient;
using Xunit;
namespace Ruach.Tests;

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
        var initialResult = initial.PerformUpgrade();
        Assert.True(initialResult.Successful, initialResult.Error?.ToString());
        var upgrade = DeployChanges.To.SqlDatabase(connection).WithScripts(first, second).JournalToSqlTable("dbo", journal).WithTransactionPerScript().Build();
        Assert.Single(upgrade.GetScriptsToExecute());
        var upgradeResult = upgrade.PerformUpgrade();
        Assert.True(upgradeResult.Successful, upgradeResult.Error?.ToString());
        Assert.Empty(upgrade.GetScriptsToExecute());
        var repeatResult = upgrade.PerformUpgrade();
        Assert.True(repeatResult.Successful, repeatResult.Error?.ToString());
        await using var sql = await new SqlConnectionFactory(connection).Open();
        Assert.Equal(1, await sql.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM dbo.{table}"));
    }

    [Fact]
    public async Task SimplifiedSchemaPreservesAccountsAndRemovesOnlyLegacyOperations()
    {
        await using var sql = await new SqlConnectionFactory(connection).Open();
        var accountTables = new[] { "Users", "Businesses", "BusinessMembers", "BusinessRequests", "AdminAudit" };
        foreach (var table in accountTables)
            Assert.NotNull(await sql.ExecuteScalarAsync<int?>("SELECT OBJECT_ID(@name, 'U')", new { name = "dbo." + table }));
        Assert.NotNull(await sql.ExecuteScalarAsync<int?>("SELECT COLUMNPROPERTY(OBJECT_ID('dbo.Users'), 'GoogleSubject', 'ColumnId')"));

        var simplifiedTables = new[] { "Items", "ItemSales", "ItemSaleLines", "BusinessExpenses", "StockEntries", "StockMovements", "ItemProcessedOperations", "ItemSyncChanges" };
        foreach (var table in simplifiedTables)
            Assert.NotNull(await sql.ExecuteScalarAsync<int?>("SELECT OBJECT_ID(@name, 'U')", new { name = "dbo." + table }));

        var legacyTables = new[] { "InventoryItems", "Products", "RecipeLines", "ProductionBatches", "Sales", "Expenses", "ProcessedOperations", "SyncChanges" };
        foreach (var table in legacyTables)
            Assert.Null(await sql.ExecuteScalarAsync<int?>("SELECT OBJECT_ID(@name, 'U')", new { name = "dbo." + table }));
    }

    [Fact]
    public async Task ExpandThenContractPreservesAccountAndBusinessRows()
    {
        var source = new SqlConnectionStringBuilder(connection);
        var database = "MashalCutover" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder(source.ConnectionString) { InitialCatalog = "master" };
        var target = new SqlConnectionStringBuilder(source.ConnectionString) { InitialCatalog = database };
        await using (var sql = new SqlConnection(master.ConnectionString))
        {
            await sql.OpenAsync();
            await sql.ExecuteAsync($"CREATE DATABASE [{database}]");
        }

        try
        {
            var root = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(root, "Ruach.BusinessAid.slnx")))
                root = Directory.GetParent(root)?.FullName ?? throw new InvalidOperationException("Solution root not found.");
            string Script(string name) => File.ReadAllText(Path.Combine(root, "database", "migrations", name));
            void Apply(string name)
            {
                var result = DeployChanges.To.SqlDatabase(target.ConnectionString)
                    .WithScripts(new SqlScript(name, Script(name))).WithTransactionPerScript().Build().PerformUpgrade();
                Assert.True(result.Successful, result.Error?.ToString());
            }

            Apply("001_InitialSchema.sql");
            Apply("003_MultiBusinessAccounts.sql");
            var user = Guid.NewGuid(); var business = Guid.NewGuid(); var legacyItem = Guid.NewGuid();
            await using (var sql = new SqlConnection(target.ConnectionString))
            {
                await sql.OpenAsync();
                await sql.ExecuteAsync("""
                    INSERT dbo.Users(Id,GoogleSubject,DisplayName,Email,EmailVerified) VALUES(@user,@subject,'Existing owner','owner@example.invalid',1);
                    INSERT dbo.Businesses(Id,OwnerUid,Name,DefaultLocation,Currency,Timezone,CreatedAt,UpdatedAt) VALUES(@business,@user,'Existing shop','Main','PHP','Asia/Manila',SYSUTCDATETIME(),SYSUTCDATETIME());
                    INSERT dbo.BusinessMembers(BusinessId,UserId,Role) VALUES(@business,@user,'Owner');
                    INSERT dbo.InventoryItems(Id,BusinessId,Name,BaseUnit,UnitKind,CurrentQuantity,AverageCostCentavos,MinimumQuantity,IsActive,CreatedAt,UpdatedAt)
                     VALUES(@legacyItem,@business,'Legacy stock','piece','count',7,100,1,1,SYSUTCDATETIME(),SYSUTCDATETIME());
                    """, new { user, business, legacyItem, subject = Guid.NewGuid().ToString() });
            }

            Apply("004_SimplifiedItemModel.sql");
            await using (var sql = new SqlConnection(target.ConnectionString))
            {
                await sql.OpenAsync();
                Assert.Equal(1, await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Users WHERE Id=@user", new { user }));
                Assert.Equal(1, await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Businesses WHERE Id=@business", new { business }));
                Assert.Equal(1, await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.InventoryItems WHERE Id=@legacyItem", new { legacyItem }));
                Assert.Equal(0, await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Items"));
                Assert.Equal(0, await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.ItemSales"));
            }

            Apply("005_RemoveLegacyOperationalModel.sql");
            await using (var sql = new SqlConnection(target.ConnectionString))
            {
                await sql.OpenAsync();
                Assert.Equal(1, await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Users WHERE Id=@user AND GoogleSubject IS NOT NULL", new { user }));
                Assert.Equal(1, await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.BusinessMembers WHERE BusinessId=@business AND UserId=@user", new { business, user }));
                Assert.Null(await sql.ExecuteScalarAsync<int?>("SELECT OBJECT_ID('dbo.InventoryItems', 'U')"));
                Assert.NotNull(await sql.ExecuteScalarAsync<int?>("SELECT OBJECT_ID('dbo.Items', 'U')"));
            }
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await using var sql = new SqlConnection(master.ConnectionString);
            await sql.OpenAsync();
            await sql.ExecuteAsync($"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]");
        }
    }
}
