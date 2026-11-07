using System.Collections;
using System.Linq.Expressions;

#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public interface IOQuery : IEnumerable
{
    Expression Expression { get; }

    IOQueryProvider Provider { get; }
}
