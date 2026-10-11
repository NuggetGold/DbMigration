using DbMigration.Core.Models;
using DbMigration.Core.Security;

namespace DbMigration.Tests;

[TestClass]
public sealed class ModelTests
{
    [TestMethod]
    public void ConnectionProfile_DisplayTarget_ForSqlite_UsesFilePath()
    {
        var profile = new ConnectionProfile { Engine = DatabaseEngine.Sqlite, FilePath = "C:\\a.db" };
        Assert.AreEqual("C:\\a.db", profile.DisplayTarget);
    }

    [TestMethod]
    public void ConnectionProfile_DisplayTarget_WithoutFile_ShowsPlaceholder()
    {
        var profile = new ConnectionProfile { Engine = DatabaseEngine.Sqlite };
        Assert.AreEqual("(файл не выбран)", profile.DisplayTarget);
    }

    [TestMethod]
    public void ConnectionProfile_DisplayTarget_ForServer_CombinesHostPortDatabase()
    {
        var profile = new ConnectionProfile
        {
            Engine = DatabaseEngine.MySql,
            Host = "db.local",
            Port = 3306,
            Database = "shop",
        };
        Assert.AreEqual("db.local:3306/shop", profile.DisplayTarget);
    }

    [TestMethod]
    public void ConnectionProfile_DisplayTarget_ForServer_WithoutPort()
    {
        var profile = new ConnectionProfile
        {
            Engine = DatabaseEngine.PostgreSql,
            Host = "db.local",
            Database = "shop",
        };
        Assert.AreEqual("db.local/shop", profile.DisplayTarget);
    }

    [TestMethod]
    public void ConnectionProfile_Clone_CopiesValuesWithoutSharingInstance()
    {
        var profile = new ConnectionProfile
        {
            Name = "n",
            Engine = DatabaseEngine.PostgreSql,
            Database = "d",
            Password = "p",
        };

        var clone = profile.Clone();

        Assert.AreNotSame(profile, clone);
        Assert.AreEqual(profile.Id, clone.Id);
        Assert.AreEqual("n", clone.Name);
        Assert.AreEqual("p", clone.Password);
    }

    [TestMethod]
    public void QueryResult_Factories_ProduceExpectedShape()
    {
        var empty = QueryResult.Empty(TimeSpan.FromMilliseconds(5));
        Assert.IsFalse(empty.HasRows);
        Assert.IsEmpty(empty.Columns);
        Assert.IsEmpty(empty.Rows);

        var nonQuery = QueryResult.NonQuery(3, TimeSpan.FromMilliseconds(1));
        Assert.AreEqual(3, nonQuery.RecordsAffected);
    }

    [TestMethod]
    public void TableSchema_QualifiedNameAndPrimaryKey()
    {
        var columns = new List<ColumnSchema>
        {
            new("b", "INTEGER", CanonicalType.Integer, false, true, false, 2),
            new("a", "INTEGER", CanonicalType.Integer, false, true, false, 1),
            new("c", "TEXT", CanonicalType.Text, true, false, false, 3),
        };

        var table = new TableSchema("t", "public", columns, Array.Empty<IndexSchema>(), Array.Empty<ForeignKeySchema>());

        Assert.AreEqual("public.t", table.QualifiedName);
        Assert.HasCount(2, table.PrimaryKey);
        Assert.AreEqual("a", table.PrimaryKey[0].Name);
        Assert.IsFalse(table.Columns[2].IsRequired);
    }

    [TestMethod]
    public void TableSchema_WithoutSchema_UsesPlainName()
    {
        var table = new TableSchema("t", null, Array.Empty<ColumnSchema>(), Array.Empty<IndexSchema>(), Array.Empty<ForeignKeySchema>());
        Assert.AreEqual("t", table.QualifiedName);
    }

    [TestMethod]
    public void MigrationResult_AggregatesRowsAndFailures()
    {
        var result = new MigrationResult(
            false,
            new[]
            {
                new TableMigrationResult("a", true, 5, TimeSpan.Zero),
                new TableMigrationResult("b", true, 7, TimeSpan.Zero),
                new TableMigrationResult("c", false, 0, TimeSpan.Zero, "boom"),
            },
            TimeSpan.FromSeconds(1));

        Assert.AreEqual(12, result.TotalRowsCopied);
        Assert.AreEqual(2, result.SucceededTables);
    }

    [TestMethod]
    public void MigrationProgress_ComputesFraction()
    {
        Assert.AreEqual(0.5, new MigrationProgress("a", 1, 2, 50, 100, "m").Fraction, 0.0001);
        Assert.AreEqual(0.0, new MigrationProgress("a", 1, 2, 50, null, "m").Fraction, 0.0001);
        Assert.AreEqual(1.0, new MigrationProgress("a", 1, 2, 200, 100, "m").Fraction, 0.0001);
    }

    [TestMethod]
    public void SqlGuardVerdict_Flags()
    {
        Assert.IsTrue(new SqlGuardVerdict(SqlGuardLevel.Destructive, "m").IsDestructive);
        Assert.IsFalse(new SqlGuardVerdict(SqlGuardLevel.Safe, "m").RequiresConfirmation);
        Assert.IsTrue(new SqlGuardVerdict(SqlGuardLevel.Warning, "m").RequiresConfirmation);
    }
}
