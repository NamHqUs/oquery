#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public sealed class SqlServerDialect : SqlDialectBase
{
    public override string Key => "SqlServer";
    public override bool SupportsRecursiveCte => true;
    public override bool RequiresRecursiveCteKeyword => false;
    public override string RenderRecursiveProjection(RecursiveProjectionContext context)
        => !context.IsField && context.ExpressionType == typeof(decimal)
            ? $"CAST({context.RenderedExpression} AS decimal(38, 18))"
            : context.RenderedExpression;
    public override IEnumerable<SqlFunctionDefintion> CreateBuiltinFunctions()
        => new SqlServerFunctionDefinitionBuiltin().CreateAllFunctions();

    protected override IReadOnlyList<string> ForbiddenKeywords =>
    [
        "ALTER", "BACKUP", "BULK", "CREATE", "DBCC", "DELETE", "DENY", "DROP",
        "EXEC", "EXECUTE", "GRANT", "INSERT", "INTO", "MERGE", "RESTORE",
        "REVOKE", "TRUNCATE", "UPDATE"
    ];
    protected override bool SupportsBracketIdentifiers => true;
    protected override bool SupportsNestedBlockComments => true;
    protected override bool DoublesBracketIdentifierClosingEscape => true;

    public override string QuoteIdentifier(string identifier)
        => $"[{identifier.Replace("]", "]]")}]";

    public override string RenderTable(Entity entity)
        => entity.IsRawSql
            ? ValidateRawSqlEntity(entity.DbTable)
            : $"{QuoteIdentifier(entity.DbSchema ?? "dbo")}.{QuoteIdentifier(entity.DbTable ?? entity.Name)}";

    public override string RenderBoolean(bool value) => value ? "1" : "0";
    public override string RenderStringLiteral(string value) => $"N'{value.Replace("'", "''")}'";

    public override string RenderStringConcatenation(string left, string right) => $"{left} + {right}";

    public override string RenderTakePrefix(int count) => $"TOP {count} ";
    public override string RenderCurrentDateTime() => "GETDATE()";

    public override string RenderPaging(int? skip, int? take)
        => skip is null
            ? string.Empty
            : $" OFFSET {skip} ROWS" +
              (take is null ? string.Empty : $" FETCH NEXT {take} ROWS ONLY");

    public override string RenderPaging(int? skip, int? take, bool hasOrderBy)
        => skip is null
            ? RenderPaging(skip, take)
            : (hasOrderBy ? string.Empty : " ORDER BY (SELECT 1)") +
              $" OFFSET {skip} ROWS" +
              (take is null ? string.Empty : $" FETCH NEXT {take} ROWS ONLY");

    public override string RenderQueryWithParameters(
        string commandText,
        IReadOnlyList<(string Name, object? Value)> parameters)
    {
        if (parameters.Count == 0)
            return commandText;

        var declarations = parameters
            .Select(parameter =>
                $"{parameter.Name} {GetSqlServerType(parameter.Value)} = {FormatParameterValue(parameter.Value)}")
            .ToArray();
        var declarationText = declarations.Length == 1
            ? "DECLARE " + declarations[0] + ";"
            : "DECLARE " + declarations[0] + ",\n\t" +
                string.Join(",\n\t", declarations.Skip(1)) + ";";

        return declarationText + "\n" + commandText;
    }

    private static string GetSqlServerType(object? value)
        => value switch
        {
            null => "nvarchar(4000)",
            string => "nvarchar(4000)",
            bool => "bit",
            byte or short or int or long => "int",
            float or double => "float",
            decimal => "decimal(38, 18)",
            DateTime or DateTimeOffset => "datetime2",
            Guid => "uniqueidentifier",
            _ => "nvarchar(4000)"
        };
}
