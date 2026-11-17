using System.Linq.Expressions;

#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public interface IOQueryProvider
{
    IOQuery CreateQuery(Expression expression);
    SqlTranslationResult GetQuery(Expression expression);
    OQueryResult ExecuteResult(Expression expression);
    IEnumerable<Dictionary<string, object?>> Execute(Expression expression);
    object? ExecuteScalar(Expression expression);
    Task<OQueryResult> ExecuteResultAsync(Expression expression, CancellationToken cancellationToken);
    Task<List<Dictionary<string, object?>>> ExecuteAsync(Expression expression, CancellationToken cancellationToken);
    Task<object?> ExecuteScalarAsync(Expression expression, CancellationToken cancellationToken);
}
