using DbMigration.Core.Models;
using DbMigration.Core.Providers;

namespace DbMigration.Tests;

[TestClass]
public sealed class CanonicalTypeMapperTests
{
    [TestMethod]
    [DataRow("INT", CanonicalType.Integer)]
    [DataRow("integer", CanonicalType.Integer)]
    [DataRow("SMALLINT", CanonicalType.Integer)]
    [DataRow("BIGINT", CanonicalType.BigInt)]
    [DataRow("VARCHAR(255)", CanonicalType.Text)]
    [DataRow("TEXT", CanonicalType.Text)]
    [DataRow("CLOB", CanonicalType.Text)]
    [DataRow("ENUM('a','b')", CanonicalType.Text)]
    [DataRow("DATETIME", CanonicalType.DateTime)]
    [DataRow("TIMESTAMP WITH TIME ZONE", CanonicalType.DateTime)]
    [DataRow("DATE", CanonicalType.Date)]
    [DataRow("TIME", CanonicalType.Time)]
    [DataRow("INTERVAL", CanonicalType.Time)]
    [DataRow("BOOL", CanonicalType.Boolean)]
    [DataRow("BIT", CanonicalType.Boolean)]
    [DataRow("TINYINT(1)", CanonicalType.Boolean)]
    [DataRow("DECIMAL(10,2)", CanonicalType.Decimal)]
    [DataRow("NUMERIC", CanonicalType.Decimal)]
    [DataRow("MONEY", CanonicalType.Decimal)]
    [DataRow("FLOAT", CanonicalType.Real)]
    [DataRow("DOUBLE PRECISION", CanonicalType.Real)]
    [DataRow("REAL", CanonicalType.Real)]
    [DataRow("UUID", CanonicalType.Uuid)]
    [DataRow("uniqueidentifier", CanonicalType.Uuid)]
    [DataRow("JSON", CanonicalType.Json)]
    [DataRow("JSONB", CanonicalType.Json)]
    [DataRow("BLOB", CanonicalType.Blob)]
    [DataRow("BYTEA", CanonicalType.Blob)]
    [DataRow("VARBINARY(MAX)", CanonicalType.Blob)]
    [DataRow("", CanonicalType.Unknown)]
    [DataRow(null, CanonicalType.Unknown)]
    [DataRow("geography", CanonicalType.Unknown)]
    public void Infer_MapsNativeTypes(string? native, CanonicalType expected)
    {
        Assert.AreEqual(expected, CanonicalTypeMapper.Infer(native));
    }

    [TestMethod]
    [DataRow("VARCHAR(255)", 255)]
    [DataRow("DECIMAL(10,2)", 10)]
    [DataRow("NUMERIC(18)", 18)]
    public void ParseDeclaredLength_ReadsFirstArgument(string declared, int expected)
    {
        Assert.AreEqual(expected, CanonicalTypeMapper.ParseDeclaredLength(declared));
    }

    [TestMethod]
    public void ParseDeclaredLength_ReturnsNull_WhenNoLength()
    {
        Assert.IsNull(CanonicalTypeMapper.ParseDeclaredLength("INT"));
        Assert.IsNull(CanonicalTypeMapper.ParseDeclaredLength(""));
        Assert.IsNull(CanonicalTypeMapper.ParseDeclaredLength(null));
    }
}
