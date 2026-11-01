#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public abstract class QueryValue
{
    public override bool Equals(object? obj) => throw new NotImplementedException();
    public override int GetHashCode() => throw new NotImplementedException();

    public T As<T>() =>  throw new NotImplementedException();
    public bool Like(object value) => throw new NotImplementedException();
    public bool NotLike(object value) => throw new NotImplementedException();
    public bool Between(object lower, object upper) => throw new NotImplementedException();
    public bool NotBetween(object lower, object upper) => throw new NotImplementedException();
    public bool In(params object[] values) => throw new NotImplementedException();
    public bool NotIn(params object[] values) => throw new NotImplementedException();
    public bool IsNull() => throw new NotImplementedException();
    public bool IsNotNull() => throw new NotImplementedException();
    public bool CompareTemplate(object value, string template) => throw new NotImplementedException();
    public static QueryValue Parameter(object? value) => throw new NotImplementedException();
    public static QueryValue QueryExpression(IFieldDefinition expression) => throw new NotImplementedException();

    public static bool operator ==(QueryValue value1, object value2) => throw new NotImplementedException();
    public static bool operator !=(QueryValue value1, object value2) => throw new NotImplementedException();
    public static bool operator >(QueryValue value1, object value2) => throw new NotImplementedException();
    public static bool operator <(QueryValue value1, object value2) => throw new NotImplementedException();
    public static bool operator >=(QueryValue value1, object value2) => throw new NotImplementedException();
    public static bool operator <=(QueryValue value1, object value2) => throw new NotImplementedException();
    public static QueryValue operator +(QueryValue value1, object value2) => throw new NotImplementedException();
    public static QueryValue operator -(QueryValue value1, object value2) => throw new NotImplementedException();
    public static QueryValue operator *(QueryValue value1, QueryValue value2) => throw new NotImplementedException();
    public static QueryValue operator /(QueryValue value1, QueryValue value2) => throw new NotImplementedException();

}
