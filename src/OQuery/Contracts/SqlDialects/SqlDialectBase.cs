using OQuery.Contracts;
using System.Data;
using System.Text.RegularExpressions;

#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public abstract class SqlDialectBase : ISqlDialect
{
    public virtual IEnumerable<SqlFunctionDefintion> CreateBuiltinFunctions() => [];

    public abstract string Key { get; }
    public abstract string QuoteIdentifier(string identifier);
    public virtual string RenderCteIdentifier(string identifier)
        => QuoteIdentifier(identifier);
    public abstract string RenderTable(Entity entity);
    public abstract string RenderBoolean(bool value);
    public abstract string RenderStringLiteral(string value);
    public abstract string RenderStringConcatenation(string left, string right);
    public abstract string RenderTakePrefix(int count);
    public abstract string RenderPaging(int? skip, int? take);
    public virtual string RenderOrderField(
        string expression,
        string? field,
        string? alias,
        int ordinal)
        => alias is null ? expression : QuoteIdentifier(alias);
    public virtual string RenderGroupField(
        string expression,
        IReadOnlyList<string> fields,
        string? alias)
        => expression;
    public virtual string RenderCurrentDateTime() => "CURRENT_TIMESTAMP";
    public virtual bool SupportsRecursiveCte => false;
    public virtual bool RequiresRecursiveCteKeyword => SupportsRecursiveCte;
    public virtual RecursiveJoinRendering RenderRecursiveJoins(
        string source,
        IReadOnlyList<RecursiveJoin> joins)
        => new(
            source + string.Concat(joins.Select(join =>
                $"\n\t{join.JoinType} {join.Table} ON {string.Join(" AND ", join.Conditions)}")),
            []);
    public virtual string RenderRecursiveProjection(RecursiveProjectionContext context)
        => context.RenderedExpression;
    public virtual string RenderDateDifferenceInDays(string startDate, string endDate)
        => $"DATEDIFF(day, {startDate}, {endDate})";

    public string FormatParameterValue(object? value)
        => SqlDialect.FormatParameterValue(value);

    public virtual object? NormalizeParameterValue(object? value) => value;

    public virtual string PrepareCommandText(
        string commandText,
        IReadOnlyList<(string Name, object? Value)> parameters)
        => commandText;

    public virtual PreparedSqlCommand PrepareCommand(
        string commandText,
        IReadOnlyList<(string Name, object? Value)> parameters)
        => new(PrepareCommandText(commandText, parameters), parameters);

    protected virtual IReadOnlyList<string> ForbiddenKeywords => [];
    protected virtual bool SupportsBracketIdentifiers => false;
    protected virtual bool SupportsBacktickIdentifiers => false;
    protected virtual bool SupportsNestedBlockComments => false;
    protected virtual bool SupportsDollarQuotedStrings => false;
    protected virtual bool DoublesBracketIdentifierClosingEscape => false;
    protected virtual bool HasBackslashEscapes(string sql, int quoteIndex) => false;

    public virtual void ValidateSqlStatement(string commandText)
    {
        var maskedSql = MaskLiteralsIdentifiersAndComments(commandText);
        var match = CreateKeywordRegex(ForbiddenKeywords.ToArray()).Match(maskedSql);
        if (match.Success)
        {
            throw new ArgumentException(
                $"SQL statement contains the disallowed keyword '{match.Value}'.",
                nameof(commandText));
        }
    }

    protected string ValidateRawSqlEntity(string rawSql)
    {
        ValidateSqlStatement(rawSql);
        return rawSql;
    }

    public virtual void ConfigureParameter(IDbDataParameter parameter, object? value)
    {
        parameter.Value = NormalizeParameterValue(value) ?? DBNull.Value;
    }

    public virtual void ConfigureCommand(IDbCommand command) { }

    public virtual string RenderPaging(int? skip, int? take, bool hasOrderBy)
        => RenderPaging(skip, take);

    public virtual string RenderOrder(string expression, bool descending)
        => $"{expression}{(descending ? " DESC" : " ASC")}";

    public virtual string RenderArithmetic(string left, string @operator, string right)
        => $"{left} {@operator} {right}";

    public virtual string RenderQueryWithParameters(
        string commandText,
        IReadOnlyList<(string Name, object? Value)> parameters)
        => parameters.Count == 0
            ? commandText
            : commandText + $"\n-- Parameters: {string.Join(", ", parameters.Select(parameter =>
                $"{parameter.Name} = {FormatParameterValue(parameter.Value)}"))}";

    private static Regex CreateKeywordRegex(params string[] keywords)
    {
        var alternatives = string.Join("|", keywords);
        return new Regex(
            $@"(?<![\p{{L}}\p{{N}}_$@#])(?:{alternatives})(?![\p{{L}}\p{{N}}_$@#])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    private string MaskLiteralsIdentifiersAndComments(string sql)
    {
        var masked = sql.ToCharArray();

        for (var index = 0; index < sql.Length;)
        {
            if (sql[index] == '-' && HasNext(sql, index, '-'))
            {
                var end = index + 2;
                while (end < sql.Length && sql[end] is not '\r' and not '\n')
                    end++;

                MaskRange(masked, index, end);
                index = end;
                continue;
            }

            if (sql[index] == '/' && HasNext(sql, index, '*'))
            {
                index = MaskBlockComment(sql, masked, index, SupportsNestedBlockComments);
                continue;
            }

            if (SupportsDollarQuotedStrings &&
                sql[index] == '$' &&
                TryGetDollarQuoteDelimiter(sql, index, out var delimiter, out var contentStart))
            {
                var closingStart = sql.IndexOf(delimiter, contentStart, StringComparison.Ordinal);
                if (closingStart < 0)
                    throw UnterminatedSqlPart("dollar-quoted string");

                var end = closingStart + delimiter.Length;
                MaskRange(masked, index, end);
                index = end;
                continue;
            }

            if (sql[index] == '\'')
            {
                var backslashEscapes = HasBackslashEscapes(sql, index);
                index = MaskDelimited(sql, masked, index, '\'', backslashEscapes);
                continue;
            }

            if (sql[index] == '"')
            {
                index = MaskDelimited(sql, masked, index, '"', backslashEscapes: false);
                continue;
            }

            if (SupportsBracketIdentifiers && sql[index] == '[')
            {
                index = MaskDelimited(
                    sql,
                    masked,
                    index,
                    ']',
                    backslashEscapes: false,
                    doubledClosingEscape: DoublesBracketIdentifierClosingEscape);
                continue;
            }

            if (SupportsBacktickIdentifiers && sql[index] == '`')
            {
                index = MaskDelimited(sql, masked, index, '`', backslashEscapes: false);
                continue;
            }

            index++;
        }

        return new string(masked);
    }

    private static int MaskBlockComment(
        string sql,
        char[] masked,
        int start,
        bool supportsNestedComments)
    {
        var depth = 1;
        var index = start + 2;

        while (index < sql.Length)
        {
            if (supportsNestedComments && sql[index] == '/' && HasNext(sql, index, '*'))
            {
                depth++;
                index += 2;
                continue;
            }

            if (sql[index] == '*' && HasNext(sql, index, '/'))
            {
                depth--;
                index += 2;
                if (depth == 0)
                {
                    MaskRange(masked, start, index);
                    return index;
                }

                continue;
            }

            index++;
        }

        throw UnterminatedSqlPart("block comment");
    }

    private static int MaskDelimited(
        string sql,
        char[] masked,
        int start,
        char closingCharacter,
        bool backslashEscapes,
        bool doubledClosingEscape = true)
    {
        var index = start + 1;

        while (index < sql.Length)
        {
            if (backslashEscapes && sql[index] == '\\')
            {
                index = Math.Min(index + 2, sql.Length);
                continue;
            }

            if (sql[index] == closingCharacter)
            {
                if (doubledClosingEscape && HasNext(sql, index, closingCharacter))
                {
                    index += 2;
                    continue;
                }

                index++;
                MaskRange(masked, start, index);
                return index;
            }

            index++;
        }

        throw UnterminatedSqlPart("quoted value or identifier");
    }

    private static bool TryGetDollarQuoteDelimiter(
        string sql,
        int start,
        out string delimiter,
        out int contentStart)
    {
        var index = start + 1;
        if (index < sql.Length && sql[index] == '$')
        {
            delimiter = "$$";
            contentStart = index + 1;
            return true;
        }

        if (index >= sql.Length || !(IsAsciiLetter(sql[index]) || sql[index] == '_'))
        {
            delimiter = string.Empty;
            contentStart = start;
            return false;
        }

        index++;
        while (index < sql.Length &&
            (IsAsciiLetter(sql[index]) || char.IsDigit(sql[index]) || sql[index] == '_'))
        {
            index++;
        }

        if (index >= sql.Length || sql[index] != '$')
        {
            delimiter = string.Empty;
            contentStart = start;
            return false;
        }

        delimiter = sql[start..(index + 1)];
        contentStart = index + 1;
        return true;
    }

    private static bool IsAsciiLetter(char value)
        => value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static bool HasNext(string text, int index, char expected)
        => index + 1 < text.Length && text[index + 1] == expected;

    private static void MaskRange(char[] text, int start, int end)
    {
        for (var index = start; index < end; index++)
        {
            if (text[index] is not '\r' and not '\n')
                text[index] = ' ';
        }
    }

    private static ArgumentException UnterminatedSqlPart(string part)
        => new($"SQL statement contains an unterminated {part}.", "sql");
}
