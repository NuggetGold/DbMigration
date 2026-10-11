using DbMigration.Core.Security;

namespace DbMigration.Tests;

[TestClass]
public sealed class SqlGuardTests
{
    [TestMethod]
    public void Evaluate_EmptySql_IsSafe()
    {
        var verdict = SqlGuard.Evaluate("   ");
        Assert.AreEqual(SqlGuardLevel.Safe, verdict.Level);
        Assert.IsFalse(verdict.RequiresConfirmation);
    }

    [TestMethod]
    public void Evaluate_Select_IsSafe()
    {
        var verdict = SqlGuard.Evaluate("SELECT id, name FROM users WHERE id = 1");
        Assert.AreEqual(SqlGuardLevel.Safe, verdict.Level);
    }

    [TestMethod]
    public void Evaluate_DropTable_IsDestructive()
    {
        var verdict = SqlGuard.Evaluate("DROP TABLE users");
        Assert.AreEqual(SqlGuardLevel.Destructive, verdict.Level);
        Assert.IsTrue(verdict.IsDestructive);
        Assert.IsTrue(verdict.RequiresConfirmation);
    }

    [TestMethod]
    public void Evaluate_Truncate_IsDestructive()
    {
        Assert.AreEqual(SqlGuardLevel.Destructive, SqlGuard.Evaluate("TRUNCATE TABLE users").Level);
    }

    [TestMethod]
    public void Evaluate_DeleteWithoutWhere_IsDestructive()
    {
        Assert.AreEqual(SqlGuardLevel.Destructive, SqlGuard.Evaluate("DELETE FROM users").Level);
    }

    [TestMethod]
    public void Evaluate_DeleteWithWhere_IsWarning()
    {
        var verdict = SqlGuard.Evaluate("DELETE FROM users WHERE id = 5");
        Assert.AreEqual(SqlGuardLevel.Warning, verdict.Level);
        Assert.IsTrue(verdict.RequiresConfirmation);
        Assert.IsFalse(verdict.IsDestructive);
    }

    [TestMethod]
    public void Evaluate_UpdateWithoutWhere_IsDestructive()
    {
        Assert.AreEqual(SqlGuardLevel.Destructive, SqlGuard.Evaluate("UPDATE users SET active = 0").Level);
    }

    [TestMethod]
    public void Evaluate_UpdateWithWhere_IsWarning()
    {
        Assert.AreEqual(SqlGuardLevel.Warning, SqlGuard.Evaluate("UPDATE users SET active = 0 WHERE id = 5").Level);
    }

    [TestMethod]
    public void Evaluate_Alter_IsWarning()
    {
        Assert.AreEqual(SqlGuardLevel.Warning, SqlGuard.Evaluate("ALTER TABLE users ADD email TEXT").Level);
    }

    [TestMethod]
    public void Evaluate_StoredProcedure_IsWarning()
    {
        Assert.AreEqual(SqlGuardLevel.Warning, SqlGuard.Evaluate("EXEC dbo.RebuildIndexes").Level);
    }

    [TestMethod]
    public void Evaluate_KeywordsInsideStringLiteral_AreIgnored()
    {
        Assert.AreEqual(SqlGuardLevel.Safe, SqlGuard.Evaluate("SELECT 'DROP TABLE users' AS note").Level);
    }

    [TestMethod]
    public void Evaluate_KeywordsInsideLineComment_AreIgnored()
    {
        Assert.AreEqual(SqlGuardLevel.Safe, SqlGuard.Evaluate("SELECT 1 -- DROP TABLE users").Level);
    }

    [TestMethod]
    public void Evaluate_KeywordsInsideBlockComment_AreIgnored()
    {
        Assert.AreEqual(SqlGuardLevel.Safe, SqlGuard.Evaluate("SELECT 1 /* DELETE FROM users */").Level);
    }

    [TestMethod]
    public void Evaluate_MultipleStatements_ReportsWorst()
    {
        Assert.AreEqual(SqlGuardLevel.Destructive, SqlGuard.Evaluate("SELECT 1; DROP TABLE users;").Level);
    }

    [TestMethod]
    public void Evaluate_MixedFindings_KeepsOnlyWorstMessages()
    {
        var verdict = SqlGuard.Evaluate("DROP TABLE users; DELETE FROM users WHERE id = 1;");
        Assert.AreEqual(SqlGuardLevel.Destructive, verdict.Level);
        StringAssert.Contains(verdict.Message, "DROP");
        Assert.IsFalse(verdict.Message.Contains("DELETE", StringComparison.Ordinal));
    }
}
