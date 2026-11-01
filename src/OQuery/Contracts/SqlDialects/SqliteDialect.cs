#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public sealed class SqliteDialect(string? schema = null) : SqlDialectBase
{
    public override string Key => "Sqlite";
    public override bool SupportsRecursiveCte => true;
    public override IEnumerable<SqlFunctionDefintion> CreateBuiltinFunctions()
        => new SqliteFunctionDefinitionBuiltin().CreateAllFunctions();

    protected override IReadOnlyList<string> ForbiddenKeywords =>
    [
        "ALTER", "ANALYZE", "ATTACH", "CREATE", "DELETE", "DETACH", "DROP",
        "INSERT", "INTO", "PRAGMA", "REINDEX", "REPLACE", "TRUNCATE", "UPDATE",
        "VACUUM"
    ];
    protected override bool SupportsBracketIdentifiers => true;
    protected override bool SupportsBacktickIdentifiers => true;

    public override string QuoteIdentifier(string identifier)
        => $"\"{identifier.Replace("\"", "\"\"")}\"";

    public override string RenderTable(Entity entity)
        => entity.IsRawSql
            ? ValidateRawSqlEntity(entity.DbTable)
            : string.IsNullOrEmpty(entity.DbSchema ?? schema)
                ? QuoteIdentifier(entity.DbTable ?? entity.Name)
                : $"{QuoteIdentifier(entity.DbSchema ?? schema!)}.{QuoteIdentifier(entity.DbTable ?? entity.Name)}";

    public override string RenderBoolean(bool value) => value ? "1" : "0";
    public override string RenderStringLiteral(string value) => $"'{value.Replace("'", "''")}'";

    public override string RenderStringConcatenation(string left, string right) => $"{left} || {right}";

    public override string RenderTakePrefix(int count) => string.Empty;
    public override string RenderCurrentDateTime() => "datetime('now', 'localtime')";
    public override string RenderDateDifferenceInDays(string startDate, string endDate)
        => $"CAST(julianday(date({endDate})) - julianday(date({startDate})) AS INTEGER)";

    public override string RenderPaging(int? skip, int? take)
    {
        if (take is null && skip is null)
            return string.Empty;

        return $" LIMIT {(take?.ToString() ?? "-1")}" +
            (skip is null ? string.Empty : $" OFFSET {skip}");
    }
}
