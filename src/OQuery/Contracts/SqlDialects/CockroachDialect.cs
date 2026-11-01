#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public sealed class CockroachDialect(string schema = "public") : SqlDialectBase
{
    public override string Key => "Cockroach";
    public override bool SupportsRecursiveCte => true;

    public override IEnumerable<SqlFunctionDefintion> CreateBuiltinFunctions()
        => new CockroachFunctionDefinition().CreateAllFunctions();

    protected override IReadOnlyList<string> ForbiddenKeywords =>
    [
        "ALTER", "CALL", "COPY", "CREATE", "DELETE", "DO", "DROP", "GRANT",
        "INSERT", "INTO", "MERGE", "REFRESH", "REVOKE", "TRUNCATE", "UPDATE",
        "VACUUM"
    ];

    protected override bool SupportsNestedBlockComments => true;
    protected override bool SupportsDollarQuotedStrings => true;

    protected override bool HasBackslashEscapes(string sql, int quoteIndex)
        => quoteIndex > 0 &&
            sql[quoteIndex - 1] is 'e' or 'E' &&
            (quoteIndex == 1 || !char.IsLetterOrDigit(sql[quoteIndex - 2]) &&
                sql[quoteIndex - 2] is not '_' and not '$');

    public override string QuoteIdentifier(string identifier)
        => $"\"{identifier.ToLowerInvariant().Replace("\"", "\"\"")}\"";

    public override string RenderTable(Entity entity)
        => entity.IsRawSql
            ? ValidateRawSqlEntity(entity.DbTable)
            : $"{QuoteIdentifier(entity.DbSchema ?? schema)}.{QuoteIdentifier(entity.DbTable ?? entity.Name)}";

    public override string RenderBoolean(bool value) => value ? "1" : "0";
    public override string RenderStringLiteral(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    public override string RenderStringConcatenation(string left, string right) => $"{left} || {right}";
    public override string RenderTakePrefix(int count) => string.Empty;
    public override string RenderDateDifferenceInDays(string startDate, string endDate)
        => $"CAST({endDate} AS DATE) - CAST({startDate} AS DATE)";

    public override string RenderPaging(int? skip, int? take)
        => take is null
            ? (skip is null ? string.Empty : $" OFFSET {skip}")
            : $" LIMIT {take}" +
              (skip is null ? string.Empty : $" OFFSET {skip}");
}
