using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

[Flags]
public enum FunctionApplyFor
{
    DataSet = 1,
    Text = 2,
    Number = 4,
    DateTime = 8,
    Boolean = 16
}

public sealed class SqlFunctionDefintion
{
    private static readonly Regex WhereConjunction = new(
        @"(?<lead>\s*)\bWHERE\s+(?<first>\{[^{}]+\}|@JoinedConditions)(?:\s+AND\s+(?<second>\{[^{}]+\}|@JoinedConditions))?",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly ReadOnlyDictionary<string, string> _sqlTemplates;

    public SqlFunctionDefintion(
        string name,
        IReadOnlyList<SqlFunctionArgumentDefinition> arguments,
        DataType resultType,
        string description,
        IReadOnlyDictionary<string, string>? sqlTemplates = null,
        bool bindArgumentsFromEnd = false,
        FunctionApplyFor functionApplyFor = FunctionApplyFor.DataSet)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A function name is required.", nameof(name));
        ArgumentNullException.ThrowIfNull(arguments);
        if (!Enum.IsDefined(resultType))
            throw new ArgumentOutOfRangeException(nameof(resultType));
        const FunctionApplyFor allApplyFor =
            FunctionApplyFor.DataSet |
            FunctionApplyFor.Text |
            FunctionApplyFor.Number |
            FunctionApplyFor.DateTime |
            FunctionApplyFor.Boolean;
        if (functionApplyFor == 0 || (functionApplyFor & ~allApplyFor) != 0)
            throw new ArgumentOutOfRangeException(nameof(functionApplyFor));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("A function description is required.", nameof(description));
        if (sqlTemplates is null || sqlTemplates.Count == 0)
            throw new ArgumentException("At least one dialect-specific SQL template is required.");
        RequiresContext = sqlTemplates.Values.Any(template => template.Contains("@Context", StringComparison.Ordinal));
        if (RequiresContext && !functionApplyFor.HasFlag(FunctionApplyFor.DataSet))
            throw new ArgumentException("A function template that uses @Context must apply to DataSet.", nameof(functionApplyFor));
        if (arguments.Any(argument => argument is null))
            throw new ArgumentException("Function arguments cannot contain null definitions.", nameof(arguments));
        if (arguments.Select(argument => argument.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != arguments.Count)
            throw new ArgumentException("Function argument names must be unique.", nameof(arguments));

        // Default: optional arguments must trail. Binding from the end: optional arguments must lead,
        // so a call with fewer arguments fills the trailing required slots.
        var optionalArgumentSeen = false;
        var requiredArgumentSeen = false;
        foreach (var argument in arguments)
        {
            optionalArgumentSeen |= argument.Optional;
            requiredArgumentSeen |= !argument.Optional;
            if (bindArgumentsFromEnd
                    ? argument.Optional && requiredArgumentSeen
                    : !argument.Optional && optionalArgumentSeen)
                throw new ArgumentException(bindArgumentsFromEnd
                    ? "Optional arguments must precede required arguments when binding from the end."
                    : "Required arguments cannot follow optional arguments.", nameof(arguments));
        }

        Name = name;
        Arguments = Array.AsReadOnly(arguments.ToArray());
        ResultType = resultType;
        IsAggregate = BuiltInAggregateFunctions.Contains(name);
        BindArgumentsFromEnd = bindArgumentsFromEnd;
        FunctionApplyFor = functionApplyFor;
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
    public bool BindArgumentsFromEnd { get; }
    public FunctionApplyFor FunctionApplyFor { get; }
    public bool RequiresContext { get; }
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

    /// <summary>
    /// Resolves the template for a dialect. <paramref name="isOmitted"/> reports whether a
    /// placeholder token ({n}, {name} or @JoinedConditions) renders empty; a WHERE clause whose
    /// conditions are all omitted is dropped, and an omitted side of an AND is removed.
    /// </summary>
    public string GetSqlTemplate(
        string dialect,
        string? context = null,
        string? joinedConditions = null,
        Func<string, bool>? isOmitted = null)
    {
        var template = GetSqlTemplate(dialect);
        if (isOmitted is not null)
            template = RemoveOmittedConjunctions(template, isOmitted);
        if (template.Contains("@Context", StringComparison.Ordinal))
            template = template.Replace(
                "@Context",
                context ?? throw new FormatException(
                    $"Function [{Name}] requires a context SQL fragment."),
                StringComparison.Ordinal);
        if (template.Contains("@JoinedConditions", StringComparison.Ordinal))
            template = template.Replace(
                "@JoinedConditions",
                joinedConditions ?? throw new FormatException(
                    $"Function [{Name}] requires joined conditions SQL."),
                StringComparison.Ordinal);
        return template;
    }

    private static string RemoveOmittedConjunctions(string template, Func<string, bool> isOmitted)
        => WhereConjunction.Replace(template, match =>
        {
            var first = match.Groups["first"].Value;
            var lead = match.Groups["lead"].Value;
            if (!match.Groups["second"].Success)
                return isOmitted(first) ? string.Empty : match.Value;

            var second = match.Groups["second"].Value;
            return (isOmitted(first), isOmitted(second)) switch
            {
                (true, true) => string.Empty,
                (true, false) => $"{lead}WHERE {second}",
                (false, true) => $"{lead}WHERE {first}",
                _ => match.Value
            };
        });

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
        public SqlFunctionArgumentDefinition(string name, DataType dataType, object? defaultValue = null, bool omittable = false)
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
            Omittable = omittable;
        }

        public string Name { get; }
        public DataType DataType { get; }
        public object? DefaultValue { get; }
        /// <summary>When true, the argument may be left out of a call and renders as an omitted fragment.</summary>
        public bool Omittable { get; }
        public bool Optional => DefaultValue is not null || Omittable;

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
