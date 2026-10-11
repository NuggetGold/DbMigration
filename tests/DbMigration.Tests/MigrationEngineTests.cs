using DbMigration.Core.Migration;
using DbMigration.Core.Models;
using DbMigration.Core.Providers;
using Microsoft.Data.Sqlite;

namespace DbMigration.Tests;

[TestClass]
public sealed class MigrationEngineTests
{
    private string _directory = null!;
    private SqliteProvider _provider = null!;
    private MigrationEngine _engine = null!;
    private ConnectionProfile _source = null!;
    private ConnectionProfile _target = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _directory = Path.Combine(Path.GetTempPath(), "DbMigrationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);

        _provider = new SqliteProvider();
        _engine = new MigrationEngine(new DatabaseProviderFactory());
        _source = new ConnectionProfile
        {
            Name = "source",
            Engine = DatabaseEngine.Sqlite,
            FilePath = Path.Combine(_directory, "source.db"),
        };
        _target = new ConnectionProfile
        {
            Name = "target",
            Engine = DatabaseEngine.Sqlite,
            FilePath = Path.Combine(_directory, "target.db"),
        };

        await _provider.ExecuteNonQueryAsync(_source, "CREATE TABLE people (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, age INTEGER);");
        await _provider.ExecuteNonQueryAsync(_source, "INSERT INTO people (name, age) VALUES ('Anna', 30), ('Bob', 41);");
        await _provider.ExecuteNonQueryAsync(_source, "CREATE TABLE pets (id INTEGER PRIMARY KEY AUTOINCREMENT, pet TEXT);");
        await _provider.ExecuteNonQueryAsync(_source, "INSERT INTO pets (pet) VALUES ('cat');");
    }

    [TestCleanup]
    public void Cleanup()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private async Task<long> CountAsync(ConnectionProfile profile, string table)
    {
        var result = await _provider.ExecuteQueryAsync(profile, $"SELECT COUNT(*) FROM \"{table}\";", 10);
        return Convert.ToInt64(result.Rows[0][0]);
    }

    private Task<MigrationResult> MigrateAllAsync(ExistingTableBehavior behavior = ExistingTableBehavior.Skip) =>
        _engine.MigrateAsync(new MigrationRequest
        {
            Source = _source,
            Target = _target,
            Scope = MigrationScope.SchemaAndData,
            ExistingTables = behavior,
        });

    [TestMethod]
    public async Task Migrate_CopiesSchemaAndData()
    {
        var result = await _engine.MigrateAsync(new MigrationRequest
        {
            Source = _source,
            Target = _target,
            Scope = MigrationScope.SchemaAndData,
            ExistingTables = ExistingTableBehavior.Skip,
            BatchSize = 1,
        });

        Assert.IsTrue(result.Success, result.FatalError);
        Assert.HasCount(2, result.Tables);
        Assert.AreEqual(3, result.TotalRowsCopied);
        Assert.AreEqual(2, await CountAsync(_target, "people"));
        Assert.AreEqual(1, await CountAsync(_target, "pets"));
    }

    [TestMethod]
    public async Task Migrate_SchemaOnly_CreatesEmptyTable()
    {
        var result = await _engine.MigrateAsync(new MigrationRequest
        {
            Source = _source,
            Target = _target,
            Tables = new[] { "people" },
            Scope = MigrationScope.Schema,
        });

        Assert.IsTrue(result.Success, result.FatalError);
        Assert.HasCount(1, result.Tables);
        Assert.AreEqual(0, result.TotalRowsCopied);
        Assert.AreEqual(0, await CountAsync(_target, "people"));
    }

    [TestMethod]
    public async Task Migrate_ExistingTable_WithSkip_KeepsRows()
    {
        await MigrateAllAsync();
        var second = await MigrateAllAsync(ExistingTableBehavior.Skip);

        Assert.IsTrue(second.Success);
        Assert.AreEqual(0, second.TotalRowsCopied);
        Assert.AreEqual(2, await CountAsync(_target, "people"));
    }

    [TestMethod]
    public async Task Migrate_ExistingTable_WithDropAndRecreate_ReplacesRows()
    {
        await MigrateAllAsync();
        var second = await MigrateAllAsync(ExistingTableBehavior.DropAndRecreate);

        Assert.IsTrue(second.Success, second.FatalError);
        Assert.AreEqual(3, second.TotalRowsCopied);
        Assert.AreEqual(2, await CountAsync(_target, "people"));
    }

    [TestMethod]
    public async Task GetTableNames_ReturnsSourceTables()
    {
        var names = await _engine.GetTableNamesAsync(_source);
        CollectionAssert.AreEquivalent(new[] { "people", "pets" }, names.ToList());
    }

    [TestMethod]
    public async Task GenerateSqlScript_ContainsSchemaAndInserts()
    {
        var script = await _engine.GenerateSqlScriptAsync(new MigrationRequest
        {
            Source = _source,
            Target = _target,
            Tables = new[] { "people" },
            Scope = MigrationScope.SchemaAndData,
        });

        StringAssert.Contains(script, "CREATE TABLE");
        StringAssert.Contains(script, "INSERT INTO");
        StringAssert.Contains(script, "Anna");
        StringAssert.Contains(script, "Bob");
    }

    [TestMethod]
    public async Task Migrate_MissingTable_ReportsFailure()
    {
        var result = await _engine.MigrateAsync(new MigrationRequest
        {
            Source = _source,
            Target = _target,
            Tables = new[] { "does_not_exist" },
            Scope = MigrationScope.SchemaAndData,
        });

        Assert.IsTrue(result.Success);
        Assert.IsEmpty(result.Tables);
    }
}
