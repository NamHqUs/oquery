using System.Collections;

#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public sealed record OQueryResult : IEnumerable<Dictionary<string, object?>>
{
    private readonly Dictionary<string, int> _fieldOrdinals;

    public OQueryResult(
        IReadOnlyList<OQueryResultField> fields,
        IReadOnlyList<object?[]> data)
    {
        Fields = Array.AsReadOnly(fields.ToArray());
        Data = Array.AsReadOnly(
            data
                .Select(row => (IReadOnlyList<object?>)Array.AsReadOnly(row.ToArray()))
                .ToArray());
        _fieldOrdinals = new Dictionary<string, int>(
            fields.Count,
            StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < fields.Count; index++)
        {
            if (!_fieldOrdinals.TryAdd(fields[index].Name, index))
                throw new ArgumentException(
                    $"Duplicate result field [{fields[index].Name}].",
                    nameof(fields));
        }
    }

    public IReadOnlyList<OQueryResultField> Fields { get; }

    public IReadOnlyList<IReadOnlyList<object?>> Data { get; }

    public int RowCount => Data.Count;

    public Dictionary<string, object?> this[int rowIndex]
        => ToDictionary(rowIndex);

    public object? this[int rowIndex, int fieldIndex]
        => Data[rowIndex][fieldIndex];

    public object? this[int rowIndex, string fieldName]
        => Data[rowIndex][_fieldOrdinals[fieldName]];

    public object? this[string fieldName, int rowIndex]
        => this[rowIndex, fieldName];

    public T? As<T>(int rowIndex, int fieldIndex)
        => ConvertValue<T>(this[rowIndex, fieldIndex]);

    public T? As<T>(int rowIndex, string fieldName)
        => ConvertValue<T>(this[rowIndex, fieldName]);

    public IReadOnlyList<T?> As<T>(string fieldName)
    {
        var fieldIndex = GetFieldIndex(fieldName);
        var values = new T?[Data.Count];

        for (var rowIndex = 0; rowIndex < Data.Count; rowIndex++)
            values[rowIndex] = ConvertValue<T>(Data[rowIndex][fieldIndex]);

        return values;
    }

    public Dictionary<string, object?> ToDictionary(int rowIndex)
    {
        var values = Data[rowIndex];
        var result = new Dictionary<string, object?>(
            Fields.Count,
            StringComparer.OrdinalIgnoreCase);

        for (var fieldIndex = 0; fieldIndex < Fields.Count; fieldIndex++)
            result[Fields[fieldIndex].Name] = values[fieldIndex];

        return result;
    }

    public IReadOnlyList<Dictionary<string, object?>> ToDictionary()
    {
        var results = new List<Dictionary<string, object?>>(Data.Count);

        for (var rowIndex = 0; rowIndex < Data.Count; rowIndex++)
            results.Add(ToDictionary(rowIndex));

        return results;
    }

    public IEnumerator<Dictionary<string, object?>> GetEnumerator()
    {
        for (var rowIndex = 0; rowIndex < Data.Count; rowIndex++)
            yield return ToDictionary(rowIndex);
    }

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    private int GetFieldIndex(string fieldName)
        => _fieldOrdinals[fieldName];

    private static T? ConvertValue<T>(object? value)
    {
        if (value is null || value is DBNull)
            return default;

        if (value is T typedValue)
            return typedValue;

        return (T)Convert.ChangeType(value, typeof(T));
    }
}

public sealed record OQueryResultField(string Name, Type Type);
