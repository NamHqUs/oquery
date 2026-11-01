#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public interface IQueryParameter
{
    QueryValue this[string fieldName] { get; }

    IOQuery Query(string fieldName);
}
