#pragma warning disable IDE0130
using OQuery;

namespace OQuery.Contracts;
#pragma warning restore IDE0130

public interface IFunctionProvider
{
    SqlFunctionDefintion GetFunction(string functionName);

    bool TryGetFunction(string functionName, out SqlFunctionDefintion? function);
}

public class FunctionProvider : IFunctionProvider
{
    private readonly Dictionary<string, SqlFunctionDefintion> _functions;

    public FunctionProvider(ISqlDialect sqlDialect, IEnumerable<SqlFunctionDefintion>? functions = null)
    {
        ArgumentNullException.ThrowIfNull(sqlDialect);

        var customFunctions = functions?.ToArray() ?? [];
        _functions = sqlDialect.CreateBuiltinFunctions()
            .ToDictionary(function => function.Name, StringComparer.OrdinalIgnoreCase);

        AddFunctions(customFunctions);
    }

    private void AddFunctions(IEnumerable<SqlFunctionDefintion>? functions)
    {
        var customFunctions = functions?.ToArray() ?? [];
        if (customFunctions
            .GroupBy(function => function.Name, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1))
            throw new ArgumentException("Function names must be unique.", nameof(functions));

        foreach (var function in customFunctions)
            _functions[function.Name] = function;
    }

    public SqlFunctionDefintion GetFunction(string functionName)
        => !string.IsNullOrWhiteSpace(functionName) && _functions.TryGetValue(functionName, out var function)
            ? function
            : throw new FormatException($"Unsupported function or argument count [{functionName}].");

    public IReadOnlyCollection<SqlFunctionDefintion> GetFunctions()
        => _functions.Values.ToArray();

    public bool TryGetFunction(string functionName, out SqlFunctionDefintion? function)
    {
        if (!string.IsNullOrWhiteSpace(functionName) &&
            _functions.TryGetValue(functionName, out var found))
        {
            function = found;
            return true;
        }

        function = null;
        return false;
    }

    public FunctionProvider RegisterFunctions(IEnumerable<SqlFunctionDefintion> functions)
    {
        ArgumentNullException.ThrowIfNull(functions);

        foreach (var function in functions)
            _functions[function.Name] = function;
        return this;
    }
}
