using OQuery.Compilation.Expressions;
using OQuery.Compilation.Models;
using OQuery.Compilation.Visitors;
using OQuery.Contracts;
using System.Linq.Expressions;

namespace OQuery.Compilation;

internal sealed record CompiledQuery(
    string CommandText,
    IReadOnlyList<DbParameter> Parameters,
    IReadOnlyList<ProjectionItem> ProjectionItems,
    string? TotalRecordsCommandText);

internal sealed class SqlQueryCompiler(
    IMetadataProvider metaProvider,
    IFunctionProvider functionProvider,
    ISqlDialect sqlDialect)
{
    internal const int TotalRecordsLimit = 1000;
    public CompiledQuery Compile(
        Expression expression,
        bool includeTotalRecords = false)
    {
        var queryContext = new QueryContext(metaProvider, functionProvider, sqlDialect);
        var tableExpression = (TableExpression)new QueryVisitor(queryContext).Visit(expression);
        var statementGenerator = new SqlStatementGenerator(sqlDialect);
        var generatedSql = statementGenerator.Generate(tableExpression, queryContext.DbParameters);
        var totalRecordsSql = includeTotalRecords
            ? statementGenerator.GenerateTotalRecords(
                tableExpression,
                queryContext.DbParameters,
                TotalRecordsLimit + 1)
            : null;

        return new CompiledQuery(
            generatedSql.CommandText,
            generatedSql.Parameters,
            tableExpression.ProjectionItems.ToArray(),
            totalRecordsSql?.CommandText);
    }
}
