using OQuery.Compilation.Expressions;
using System.Linq.Expressions;

namespace OQuery.Compilation.Visitors;

class QueryVisitor(QueryContext queryContext) : ExpressionVisitor
{
    protected readonly QueryContext QueryContext = queryContext;

    private readonly Lazy<ParameterVisitor> _parameterVisitor =
        new(() => new ParameterVisitor(queryContext));
    private readonly Lazy<ClauseVisitor> _clauseVisitor =
        new(() => new ClauseVisitor(queryContext));

    protected override Expression VisitMember(MemberExpression node)
    {
        LambdaExpression lambda = Expression.Lambda(node);
        Delegate fn = lambda.Compile();
        return this.VisitConstant(Expression.Constant(fn.DynamicInvoke(null), node.Type));
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        var declaringType = node.Method.DeclaringType;

        if (declaringType == typeof(FluentQueryExtensions) || declaringType == typeof(FluentQueryExtensions.QuerySignatures))
            return _clauseVisitor.Value.Eval(node);

        if (declaringType == typeof(IQueryParameter))
            return _parameterVisitor.Value.Eval(node);

        var instance = Visit(node.Object)!;
        if (instance.Type != node.Method.DeclaringType)
            instance = Expression.Convert(instance, node.Method.DeclaringType!);

        return Expression.Call(instance, node.Method, node.Arguments);
    }

    protected override Expression VisitExtension(Expression node)
        => node is QueryDefinitionExpression queryDefinition
            ? new QueryDefinitionVisitor(QueryContext).Eval(queryDefinition.Definition)
            : base.VisitExtension(node);
}
