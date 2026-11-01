using System.Linq.Expressions;

namespace OQuery.Compilation.Utilities;

static class  ExpressionTypeExtension
{
    public static string ToSql(this ExpressionType type)
    {
        return type == ExpressionType.AndAlso ? "AND" :
        type == ExpressionType.OrElse ? "OR" :
        type == ExpressionType.Equal ? "=" :
        type == ExpressionType.NotEqual ? "<>" :
        type == ExpressionType.LessThan ? "<" :
        type == ExpressionType.LessThanOrEqual ? "<=" :
        type == ExpressionType.GreaterThan ? ">" :
        type == ExpressionType.GreaterThanOrEqual ? ">=" :

        type == ExpressionType.Add ? "+" :
        type == ExpressionType.Subtract ? "-" :
        type == ExpressionType.Multiply ? "*" :
        type == ExpressionType.Divide ? "/" :
            throw new Exception($"Cannot translate operator [{type}].");
    }
}
