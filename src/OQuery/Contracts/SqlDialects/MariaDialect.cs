#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public sealed class MariaDialect(string? schema = null) : SqlDialectBase
{
    public override string Key => "Maria";
    public override bool SupportsRecursiveCte => true;

    public override IEnumerable<SqlFunctionDefintion> CreateBuiltinFunctions()
        => new MariaFunctionDefinitionBuiltin().CreateAllFunctions();

    protected override IReadOnlyList<string> ForbiddenKeywords =>
    [
        "ALTER", "CREATE", "DELETE", "DROP", "INSERT", "INTO", "REPLACE",
        "TRUNCATE", "UPDATE", "USE"
    ];

    protected override bool SupportsBacktickIdentifiers => true;

    public override string QuoteIdentifier(string identifier)
        => $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`";

    public override string RenderTable(Entity entity)
        => entity.IsRawSql
            ? ValidateRawSqlEntity(entity.DbTable)
            : QuoteIdentifier(schema ?? entity.DbTable ?? entity.Name);

    public override string RenderBoolean(bool value) => value ? "1" : "0";
    public override string RenderStringLiteral(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    public override string RenderStringConcatenation(string left, string right) => $"CONCAT({left}, {right})";
    public override string RenderTakePrefix(int count) => string.Empty;
    public override string RenderCurrentDateTime() => "CURRENT_TIMESTAMP";
    public override string RenderDateDifferenceInDays(string startDate, string endDate)
        => $"DATEDIFF({endDate}, {startDate})";

    public override string RenderPaging(int? skip, int? take)
    {
        if (take is null && skip is null)
            return string.Empty;

        return $" LIMIT {(take?.ToString() ?? "18446744073709551615")}" +
            (skip is null ? string.Empty : $" OFFSET {skip}");
    }
}
