using OQuery.Compilation.Expressions;
using OQuery.Compilation.Models;

namespace OQuery.Compilation;

internal sealed class CompilationState
{
    public int AliasCount { get; set; }
    public bool ParseContextAggregatesAsScalar { get; set; }
    /// <summary>Grouped aggregates currently being parsed; the top scope is the innermost receiver row set.</summary>
    public Stack<FunctionAggregateScope> AggregateScopes { get; } = new();
    public Stack<(string ArgumentName, TableExpression TableContext)> Arguments { get; } = new();
    public List<DbParameter> DbParameters { get; } = [];
}

/// <summary>
/// The grouped derived table an aggregate is evaluated in, and the query row it was joined from.
/// For a root-form aggregate (no receiver), bare names belong to that query row, not to the receiver.
/// </summary>
internal sealed class FunctionAggregateScope(
    GroupedAggregateTableExpression grouped,
    TableExpression outerContext,
    Relation relation)
{
    public GroupedAggregateTableExpression Grouped { get; } = grouped;
    public TableExpression OuterContext { get; } = outerContext;
    public Relation Relation { get; } = relation;
}
