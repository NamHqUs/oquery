using Microsoft.Data.Sqlite;
using Npgsql;

namespace OQuery.UnitTest.Tests.Sql;

[TestFixture]
public sealed class SqlDialectRegistryTests
{
    [TestCase("postgresql", typeof(PostgresDialect))]
    [TestCase("MariaDB", typeof(MariaDialect))]
    [TestCase("HANA", typeof(SapHanaDialect))]
    public void ForProvider_ResolvesNamesAndAliasesIgnoringCase(string provider, Type expectedDialect)
        => Assert.That(SqlDialect.ForProvider(provider), Is.InstanceOf(expectedDialect));

    [Test]
    public void ForProvider_ThrowsForUnregisteredProvider()
        => Assert.That(() => SqlDialect.ForProvider("unknown-db"), Throws.ArgumentException);

    [TestCase(typeof(SqliteConnection), typeof(SqliteDialect))]
    [TestCase(typeof(NpgsqlConnection), typeof(PostgresDialect))]
    public void ForConnection_MatchesConnectionTypeMarker(Type connectionType, Type expectedDialect)
    {
        using var connection = (IDisposable)Activator.CreateInstance(connectionType)!;

        Assert.That(SqlDialect.ForConnection((System.Data.IDbConnection)connection), Is.InstanceOf(expectedDialect));
    }
}
