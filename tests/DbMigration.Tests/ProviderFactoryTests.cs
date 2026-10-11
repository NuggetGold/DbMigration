using DbMigration.Core.Abstractions;
using DbMigration.Core.Models;
using DbMigration.Core.Providers;

namespace DbMigration.Tests;

[TestClass]
public sealed class ProviderFactoryTests
{
    [TestMethod]
    public void DefaultFactory_RegistersEveryEngine()
    {
        var factory = new DatabaseProviderFactory();

        foreach (var engine in Enum.GetValues<DatabaseEngine>())
        {
            Assert.IsTrue(factory.TryGet(engine, out var provider), engine.ToString());
            Assert.AreEqual(engine, provider.Engine);
        }

        Assert.HasCount(4, factory.All);
    }

    [TestMethod]
    public void Get_UnknownEngine_ThrowsNotSupported()
    {
        var factory = new DatabaseProviderFactory(new IDatabaseProvider[] { new SqliteProvider() });

        var threw = false;
        try
        {
            factory.Get(DatabaseEngine.MySql);
        }
        catch (NotSupportedException)
        {
            threw = true;
        }

        Assert.IsTrue(threw);
    }

    [TestMethod]
    public void Constructor_DuplicateEngine_KeepsLastProvider()
    {
        var first = new SqliteProvider();
        var second = new SqliteProvider();
        var factory = new DatabaseProviderFactory(new IDatabaseProvider[] { first, second });

        Assert.HasCount(1, factory.All);
        Assert.AreSame(second, factory.Get(DatabaseEngine.Sqlite));
    }

    [TestMethod]
    public void SqliteProvider_ReportsFileBased()
    {
        var provider = new SqliteProvider();
        Assert.IsTrue(provider.IsFileBased);
        Assert.AreEqual("SQLite", provider.DisplayName);
    }
}
