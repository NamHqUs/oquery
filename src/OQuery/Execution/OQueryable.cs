using System.Collections;
using System.Diagnostics;
using System.Linq.Expressions;

namespace OQuery.Execution;

class OQueryable : IOQuery, IEnumerable<Dictionary<string, object?>>, IEnumerable
{
    public readonly OQueryProvider Provider;
    private int _toStringDepth;

    IOQueryProvider IOQuery.Provider => Provider;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public Expression Expression { get; }

    internal OQueryable(OQueryProvider provider, Expression? expression = null)
    {
        Provider = provider;
        Expression = expression ?? Expression.Constant(this);
    }

    public IEnumerator<Dictionary<string, object?>> GetEnumerator()
        => Provider.Execute(Expression).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => Provider.Execute(Expression).GetEnumerator();

    public override string ToString()
    {
        if (_toStringDepth > 0)
            return nameof(OQueryable);

        try
        {
            _toStringDepth++;
            return Provider.GetQueryText(Expression);
        }
        finally
        {
            _toStringDepth--;
        }
    }
}
