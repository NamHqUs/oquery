using System.Globalization;
using System.Reflection;

namespace OQuery.Execution;

internal static class StrongTypeMaterializer
{
    public static T Materialize<T>(Dictionary<string, object?> record)
        where T : new()
    {
        var result = new T();
        var properties = typeof(T)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanWrite)
            .ToDictionary(property => property.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var field in record)
        {
            if (!properties.TryGetValue(field.Key, out var property))
            {
                throw new InvalidOperationException(
                    $"The result field [{field.Key}] does not map to a writable property on [{typeof(T).FullName}].");
            }

            property.SetValue(result, ConvertValue(field.Value, property.PropertyType, field.Key));
        }

        return result;
    }

    private static object? ConvertValue(object? value, Type targetType, string fieldName)
    {
        if (value is null or DBNull)
        {
            if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null)
            {
                throw new InvalidOperationException(
                    $"The result field [{fieldName}] is null but the target property type [{targetType.FullName}] is not nullable.");
            }

            return null;
        }

        var nullableType = Nullable.GetUnderlyingType(targetType);
        var nonNullableType = nullableType ?? targetType;

        if (nonNullableType.IsInstanceOfType(value))
            return value;

        if (nonNullableType.IsEnum)
        {
            if (value is string text)
                return Enum.Parse(nonNullableType, text, ignoreCase: true);

            return Enum.ToObject(nonNullableType, value);
        }

        if (nonNullableType == typeof(Guid))
            return value is string guidText
                ? Guid.Parse(guidText)
                : new Guid(Convert.ToString(value, CultureInfo.InvariantCulture)!);

        try
        {
            return Convert.ChangeType(value, nonNullableType, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            throw new InvalidOperationException(
                $"The result field [{fieldName}] with value type [{value.GetType().FullName}] cannot be converted to [{targetType.FullName}].",
                exception);
        }
    }
}
