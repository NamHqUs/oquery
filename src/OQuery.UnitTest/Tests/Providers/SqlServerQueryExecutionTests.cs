using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using OQuery.UnitTest.Setup;

using OQuery.QueryDefinitions.Parsers;

namespace OQuery.UnitTest.Tests.Providers;

[TestFixture]
internal sealed class SqlServerQueryExecutionTests
{
    private IDbConnection _connection = null!;
    private OQueryEngine _db = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        try
        {
            _connection = TestDatabase.GetDbConnection(DbServerType.SqlServer);
        }
        catch (Exception exception) when (exception is SqlException or ArgumentException)
        {
            Assert.Ignore($"SQL Server comparison requires the Docker SQL Server: {exception.Message}");
        }

        var metadata = MetadataLoader.UnitTestData
            .Concat(MetadataLoader.SaleDemoData)
            .Concat(MetadataLoader.NorthStarDemoData)
            .GroupBy(entity => entity.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last());

        _db = new OQueryEngine(
            _connection,
            new MetadataProvider(metadata),
            new FunctionProvider(SqlDialect.ForConnection(_connection)),
            SqlDialect.ForConnection(_connection));
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _db?.Dispose();
    }

    [TestCaseSource(typeof(SavedQueryTestCases), nameof(SavedQueryTestCases.Cases))]
    public void SavedQueryExecutesOnSqlServer(string fileName)
    {
        var query = QueryDefinitionLoader.Load(SavedQueryTestCases.GetRequestPath(fileName));
        Assert.That(() => _db.Query(query).ToList(), Throws.Nothing, fileName);
    }

    [Test]
    public void DiagnosisBurdenByClinicOQueryGeneratesTheSameSqlAsJson()
    {
        var requestRoot = Path.Combine(
            GetRepositoryRoot(),
            "data-storage", "queries");
        var jsonQuery = QueryDefinitionLoader.Load(
            Path.Combine(requestRoot, "Health 1. Diagnosis burden by clinic.json"));
        var oqueryPath = Path.Combine(requestRoot, "Health 1. Diagnosis burden by clinic.oqr");
        var oqueryQuery = QueryDefinitionOQueryParser.Parse(new FileInfo(oqueryPath));

        var jsonSql = _db.Query(jsonQuery).ToSql();
        var oquerySql = _db.Query(oqueryQuery).ToSql();

        Assert.That(NormalizeSql(jsonSql), Is.EqualTo(NormalizeSql(oquerySql)));
    }

    private static string GetRepositoryRoot()
        => new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "../../../../../")).FullName;

    private static string NormalizeSql(SqlTranslationResult query)
    {
        var commandText = Regex.Replace(
            query.CommandText,
            @"^DECLARE [^\r\n]+;\r?\n",
            string.Empty,
            RegexOptions.Multiline);

        foreach (var parameter in query.Parameters)
        {
            commandText = commandText.Replace(
                parameter.Name,
                FormatSqlLiteral(parameter.Value),
                StringComparison.Ordinal);
        }

        return commandText;
    }

    private static string FormatSqlLiteral(object? value)
        => value switch
        {
            null => "NULL",
            bool boolean => boolean ? "1" : "0",
            string text => $"'{text.Replace("'", "''", StringComparison.Ordinal)}'",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
}
