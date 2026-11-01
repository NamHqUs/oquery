using System.Collections.ObjectModel;

#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public sealed class SqlFunctionDefintion
{
    private readonly ReadOnlyDictionary<string, string> _sqlTemplates;

    public SqlFunctionDefintion(
        string name,
        IReadOnlyList<SqlFunctionArgumentDefinition> arguments,
        DataType resultType,
        string description,
        IReadOnlyDictionary<string, string>? sqlTemplates = null,
        bool isAggregate = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A function name is required.", nameof(name));
        ArgumentNullException.ThrowIfNull(arguments);
        if (!Enum.IsDefined(resultType))
            throw new ArgumentOutOfRangeException(nameof(resultType));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("A function description is required.", nameof(description));
        if (sqlTemplates is null || sqlTemplates.Count == 0)
            throw new ArgumentException("At least one dialect-specific SQL template is required.");
        if (arguments.Any(argument => argument is null))
            throw new ArgumentException("Function arguments cannot contain null definitions.", nameof(arguments));
        if (arguments.Select(argument => argument.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != arguments.Count)
            throw new ArgumentException("Function argument names must be unique.", nameof(arguments));

        var optionalArgumentSeen = false;
        foreach (var argument in arguments)
        {
            optionalArgumentSeen |= argument.Optional;
            if (!argument.Optional && optionalArgumentSeen)
                throw new ArgumentException("Required arguments cannot follow optional arguments.", nameof(arguments));
        }

        Name = name;
        Arguments = Array.AsReadOnly(arguments.ToArray());
        ResultType = resultType;
        IsAggregate = isAggregate;
        Description = description;

        var templates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (dialect, template) in sqlTemplates ?? new Dictionary<string, string>())
        {
            if (string.IsNullOrWhiteSpace(dialect))
                throw new ArgumentException("Dialect template keys cannot be empty.", nameof(sqlTemplates));
            if (string.IsNullOrWhiteSpace(template))
                throw new ArgumentException($"The SQL template for [{dialect}] cannot be empty.", nameof(sqlTemplates));
            ValidateTemplate(template, arguments, dialect);
            templates.Add(dialect, template);
        }

        _sqlTemplates = new ReadOnlyDictionary<string, string>(templates);
    }

    public string Name { get; }
    public IReadOnlyList<SqlFunctionArgumentDefinition> Arguments { get; }
    public DataType ResultType { get; }
    public bool IsAggregate { get; }
    public string Description { get; }
    public IReadOnlyDictionary<string, string> SqlTemplates => _sqlTemplates;

    public override string ToString() => $"{Name}({string.Join(", ", Arguments.Select(arg => $"{arg.Name}: {arg.DataType}"))}) : {ResultType}";

    public string GetSqlTemplate(string dialect)
        => _sqlTemplates.TryGetValue(dialect, out var template)
            ? template
            : (dialect.Equals("Firebird", StringComparison.OrdinalIgnoreCase) ||
               dialect.Equals("Oracle", StringComparison.OrdinalIgnoreCase)) &&
              _sqlTemplates.TryGetValue("Postgres", out var postgresTemplate)
                ? postgresTemplate
            : throw new FormatException($"Function [{Name}] has no SQL template for dialect [{dialect}].");

    private static void ValidateTemplate(
        string template,
        IReadOnlyList<SqlFunctionArgumentDefinition> arguments,
        string dialect)
    {
        for (var index = 0; index < template.Length; index++)
        {
            if (template[index] == '}')
                throw new ArgumentException($"The SQL template for [{dialect}] contains an unmatched closing brace.");
            if (template[index] != '{')
                continue;

            var close = template.IndexOf('}', index + 1);
            if (close < 0)
                throw new ArgumentException($"The SQL template for [{dialect}] contains an invalid argument placeholder.");

            var placeholder = template.Substring(index + 1, close - index - 1);
            var isIndex = int.TryParse(placeholder, out var argumentIndex) &&
                argumentIndex >= 1 && argumentIndex <= arguments.Count;
            var isName = arguments.Any(argument =>
                string.Equals(placeholder, argument.Name, StringComparison.OrdinalIgnoreCase));
            if (!isIndex && !isName)
                throw new ArgumentException($"The SQL template for [{dialect}] contains an invalid argument placeholder.");

            index = close;
        }
    }

    public sealed record SqlFunctionArgumentDefinition
    {
        public SqlFunctionArgumentDefinition(string name, DataType dataType, object? defaultValue = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("An argument name is required.", nameof(name));
            if (!Enum.IsDefined(dataType))
                throw new ArgumentOutOfRangeException(nameof(dataType));
            if (defaultValue is not null && defaultValue is not "*" && !IsDefaultValueType(dataType, defaultValue))
                throw new ArgumentException($"The default value must match argument data type [{dataType}].", nameof(defaultValue));

            Name = name;
            DataType = dataType;
            DefaultValue = defaultValue;
        }

        public string Name { get; }
        public DataType DataType { get; }
        public object? DefaultValue { get; }
        public bool Optional => DefaultValue is not null;

        private static bool IsDefaultValueType(DataType dataType, object value)
            => dataType switch
            {
                DataType.String => value is string,
                DataType.Number => value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal,
                DataType.Datetime => value is DateTime,
                DataType.Boolean => value is bool,
                _ => false
            };
    }
}
