using System.Data;
using System.Globalization;

#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130
public interface ISqlDialect
{
    string Key
        => throw new NotSupportedException("A custom SQL dialect must provide a dialect identifier.");
    IEnumerable<SqlFunctionDefintion> CreateBuiltinFunctions() => [];
    string QuoteIdentifier(string identifier);
    string RenderCteIdentifier(string identifier)
        => QuoteIdentifier(identifier);
    string RenderTable(Entity entity);
    string RenderSubqueryAlias(string alias)
        => $"AS {alias}";
    string RenderBoolean(bool value);
    string RenderStringLiteral(string value);
    string RenderStringConcatenation(string left, string right);
    string RenderArithmetic(string left, string @operator, string right)
        => $"{left} {@operator} {right}";
    string RenderTakePrefix(int count);
    string RenderPaging(int? skip, int? take);
    string RenderOrderField(
        string expression,
        string? field,
        string? alias,
        int ordinal)
        => alias is null ? expression : QuoteIdentifier(alias);
    string RenderGroupField(
        string expression,
        IReadOnlyList<string> fields,
        string? alias)
        => expression;
    string RenderOrder(string expression, bool descending)
        => $"{expression}{(descending ? " DESC" : " ASC")}";
    string FormatParameterValue(object? value);
    object? NormalizeParameterValue(object? value) => value;
    string PrepareCommandText(
        string commandText,
        IReadOnlyList<(string Name, object? Value)> parameters)
        => commandText;
    PreparedSqlCommand PrepareCommand(
        string commandText,
        IReadOnlyList<(string Name, object? Value)> parameters)
        => new(
            PrepareCommandText(commandText, parameters),
            parameters);
    void ValidateSqlStatement(string commandText) { }
    void ConfigureParameter(IDbDataParameter parameter, object? value)
    {
        parameter.Value = NormalizeParameterValue(value) ?? DBNull.Value;
    }

    void ConfigureCommand(IDbCommand command) { }
    string RenderCurrentDateTime() => "CURRENT_TIMESTAMP";
    bool SupportsRecursiveCte => false;
    bool RequiresRecursiveCteKeyword => SupportsRecursiveCte;
    RecursiveJoinRendering RenderRecursiveJoins(
        string source,
        IReadOnlyList<RecursiveJoin> joins)
        => new(
            source + string.Concat(joins.Select(join =>
                $"\n\t{join.JoinType} {join.Table} ON {string.Join(" AND ", join.Conditions)}")),
            []);
    string RenderRecursiveProjection(RecursiveProjectionContext context)
        => context.RenderedExpression;
    string RenderDateDifferenceInDays(string startDate, string endDate)
        => $"DATEDIFF(day, {startDate}, {endDate})";

    string RenderPaging(int? skip, int? take, bool hasOrderBy)
        => RenderPaging(skip, take);

    string RenderQueryWithParameters(
        string commandText,
        IReadOnlyList<(string Name, object? Value)> parameters)
        => parameters.Count == 0
            ? commandText
            : commandText + $"\n-- Parameters: {string.Join(", ", parameters.Select(parameter =>
                $"{parameter.Name} = {FormatParameterValue(parameter.Value)}"))}";
}

public sealed record PreparedSqlCommand(
    string CommandText,
    IReadOnlyList<(string Name, object? Value)> Parameters);

public sealed record RecursiveJoin(
    string JoinType,
    string Table,
    IReadOnlyList<string> Conditions);

public sealed record RecursiveJoinRendering(
    string FromClause,
    IReadOnlyList<string> AdditionalWhereConditions);

public sealed record RecursiveProjectionContext(
    Type ExpressionType,
    bool IsField,
    string RenderedExpression);
