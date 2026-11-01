using System.Data;

#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public sealed class Db2Dialect(string schema = "MAIN") : SqlDialectBase
{
    public override string Key => "Db2";
    public override bool SupportsRecursiveCte => true;
    public override bool RequiresRecursiveCteKeyword => false;
    public override RecursiveJoinRendering RenderRecursiveJoins(
        string source,
        IReadOnlyList<RecursiveJoin> joins)
        => new(
            source + (joins.Count == 0 ? string.Empty : ", " + string.Join(", ", joins.Select(join => join.Table))),
            joins.SelectMany(join => join.Conditions).ToArray());

    public override IEnumerable<SqlFunctionDefintion> CreateBuiltinFunctions()
        => new Db2FunctionDefinition().CreateAllFunctions();

    protected override IReadOnlyList<string> ForbiddenKeywords =>
    [
        "ALTER", "CALL", "CREATE", "DELETE", "DROP", "GRANT", "INSERT", "INTO",
        "MERGE", "RENAME", "REVOKE", "TRUNCATE", "UPDATE"
    ];

    public override string QuoteIdentifier(string identifier)
        => $"\"{identifier.ToUpperInvariant().Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    public override string RenderTable(Entity entity)
        => entity.IsRawSql
            ? ValidateRawSqlEntity(entity.DbTable)
            : $"{QuoteIdentifier(entity.DbSchema ?? schema)}.{QuoteIdentifier(entity.DbTable ?? entity.Name)}";

    public override string RenderBoolean(bool value) => value ? "1" : "0";
    public override string RenderStringLiteral(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    public override string RenderStringConcatenation(string left, string right) => $"{left} || {right}";
    public override string RenderArithmetic(string left, string @operator, string right)
        => @operator == "/"
            ? left.Contains("DAYS(", StringComparison.OrdinalIgnoreCase)
                ? $"({left}) / CAST({right} AS DECIMAL(5, 2))"
                : $"DECIMAL({left}, 31, 15) / {right}"
            : base.RenderArithmetic(left, @operator, right);
    public override string RenderOrder(string expression, bool descending)
        => $"{base.RenderOrder(expression, descending)}{(descending ? " NULLS LAST" : string.Empty)}";
    public override string RenderOrderField(
        string expression,
        string? field,
        string? alias,
        int ordinal)
        => alias is null ? expression : ordinal.ToString();

    public override string RenderGroupField(
        string expression,
        IReadOnlyList<string> fields,
        string? alias)
        => fields.Count > 0 &&
            (expression.StartsWith("ROUND", StringComparison.OrdinalIgnoreCase) ||
             expression.StartsWith("COALESCE", StringComparison.OrdinalIgnoreCase))
            ? fields[0]
            : fields.Count > 0 &&
                expression.Contains("||", StringComparison.Ordinal)
            ? string.Join(", ", fields)
            : expression;
    public override string RenderTakePrefix(int count) => string.Empty;
    public override string RenderCurrentDateTime() => "CURRENT TIMESTAMP";

    public override string RenderDateDifferenceInDays(string startDate, string endDate)
        => $"DAYS(CAST({endDate} AS DATE)) - DAYS(CAST({startDate} AS DATE))";

    public override string RenderPaging(int? skip, int? take)
    {
        if (take is null && skip is null)
            return string.Empty;

        return (skip is null ? string.Empty : $" OFFSET {skip} ROWS") +
            (take is null ? string.Empty : $" FETCH FIRST {take} ROWS ONLY");
    }
}
