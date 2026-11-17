using System.Linq.Expressions;

namespace OQuery.Compilation.Expressions;

internal sealed class QueryDefinitionExpression(QueryDefinition definition) : Expression
{
    public QueryDefinition Definition { get; } = definition;

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => typeof(IOQuery);
}
