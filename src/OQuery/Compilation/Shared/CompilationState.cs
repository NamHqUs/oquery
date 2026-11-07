using OQuery.Compilation.Expressions;
using OQuery.Compilation.Models;

namespace OQuery.Compilation;

internal sealed class CompilationState
{
    public int AliasCount { get; set; }
    public bool ParseContextAggregatesAsScalar { get; set; }
    public Stack<(string ArgumentName, TableExpression TableContext)> Arguments { get; } = new();
    public List<DbParameter> DbParameters { get; } = [];
}
