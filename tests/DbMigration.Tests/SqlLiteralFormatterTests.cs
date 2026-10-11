using System.Globalization;
using DbMigration.Core.Models;
using DbMigration.Core.Sql;

namespace DbMigration.Tests;

[TestClass]
public sealed class SqlLiteralFormatterTests
{
    [TestMethod]
    public void Format_NullValues_BecomeNullKeyword()
    {
        Assert.AreEqual("NULL", SqlLiteralFormatter.Format(null, DatabaseEngine.Sqlite));
        Assert.AreEqual("NULL", SqlLiteralFormatter.Format(DBNull.Value, DatabaseEngine.Sqlite));
    }

    [TestMethod]
    public void Format_String_EscapesSingleQuotes()
    {
        Assert.AreEqual("'O''Brien'", SqlLiteralFormatter.Format("O'Brien", DatabaseEngine.MySql));
    }

    [TestMethod]
    public void Format_Boolean_UsesPostgresKeywordOrNumeric()
    {
        Assert.AreEqual("TRUE", SqlLiteralFormatter.Format(true, DatabaseEngine.PostgreSql));
        Assert.AreEqual("FALSE", SqlLiteralFormatter.Format(false, DatabaseEngine.PostgreSql));
        Assert.AreEqual("1", SqlLiteralFormatter.Format(true, DatabaseEngine.MySql));
        Assert.AreEqual("0", SqlLiteralFormatter.Format(false, DatabaseEngine.SqlServer));
    }

    [TestMethod]
    public void Format_Bytes_UsesEngineSpecificSyntax()
    {
        var bytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        Assert.AreEqual("0xDEADBEEF", SqlLiteralFormatter.Format(bytes, DatabaseEngine.SqlServer));
        Assert.AreEqual("X'DEADBEEF'", SqlLiteralFormatter.Format(bytes, DatabaseEngine.Sqlite));
    }

    [TestMethod]
    public void Format_Guid_IsQuotedLowercase()
    {
        var guid = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Assert.AreEqual("'11111111-2222-3333-4444-555555555555'", SqlLiteralFormatter.Format(guid, DatabaseEngine.Sqlite));
    }

    [TestMethod]
    public void Format_DateTime_UsesInvariantFullPrecision()
    {
        var value = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Unspecified);
        Assert.AreEqual("'2024-01-02 03:04:05.0000000'", SqlLiteralFormatter.Format(value, DatabaseEngine.Sqlite));
    }

    [TestMethod]
    public void Format_Number_UsesInvariantCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.AreEqual("3.5", SqlLiteralFormatter.Format(3.5, DatabaseEngine.Sqlite));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [TestMethod]
    public void Quote_DoublesApostrophes()
    {
        Assert.AreEqual("'a''b'", SqlLiteralFormatter.Quote("a'b"));
    }
}
