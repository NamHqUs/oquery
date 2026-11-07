using System.Diagnostics;
using System.Linq.Expressions;

namespace OQuery.Compilation.Expressions;

[DebuggerDisplay("{DebugView,nq}")]
abstract class SqlExpression(Type type) : Expression
{
    public override ExpressionType NodeType => (ExpressionType)1000;
    public override Type Type { get; } = type;

    internal virtual string DebugView
        => $"{GetType().Name} ({Type.Name})";
}
