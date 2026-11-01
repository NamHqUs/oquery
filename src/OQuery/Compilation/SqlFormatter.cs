using System.Text;

namespace OQuery.Compilation;

internal static class SqlFormatter
{
    private static readonly string[] LineBreakKeywords =
    [
        "SELECT",
        "FROM",
        "WHERE",
        "AND",
        "OR",
        "ORDER",
        "GROUP",
        "HAVING",
        "GROUP BY",
        "ORDER BY",
        "HAVING",
        "UNION"
    ];
    private static readonly string[] JoinKeywords =
    [
        "LEFT",
        "RIGHT",
        "INNER",
        "FULL",
        "CROSS"
    ];

    public static string Format(string sql)
    {
        var result = new StringBuilder();
        var token = new StringBuilder();
        var indent = 0;
        var inSingleQuote = false;
        var inDoubleQuote = false;
        var inBracketQuote = false;
        var lineHasContent = false;
        var inJoin = false;
        var inCteList = false;
        var afterWith = false;

        void NewLine(int additionalIndent = 0)
        {
            while (result.Length > 0 && result[^1] == ' ')
                result.Length--;

            if (lineHasContent)
                result.AppendLine();

            result.Append(' ', (indent + additionalIndent) * 4);
            lineHasContent = false;
        }

        void WriteToken(string value)
        {
            if (value.Length == 0)
                return;

            var keyword = value.ToUpperInvariant();
            if (keyword == "WITH")
            {
                NewLine();
                result.Append(value);
                lineHasContent = true;
                afterWith = true;
                return;
            }

            if (inCteList && indent == 0 && keyword == "SELECT")
                inCteList = false;

            if (JoinKeywords.Contains(keyword))
            {
                NewLine();
                result.Append('\t');
                inJoin = true;
            }
            else if (keyword == "WHERE")
            {
                NewLine();
                inJoin = false;
            }
            else if (keyword == "AND" && inJoin)
            {
                if (lineHasContent && result[^1] != ' ')
                    result.Append(' ');
            }
            else if (keyword == "AND")
            {
                NewLine();
                result.Append('\t');
            }
            else if (LineBreakKeywords.Contains(keyword))
                NewLine();
            else if (value != "." && lineHasContent && result[^1] != ' ' && result[^1] != '(')
                result.Append(' ');

            result.Append(value);
            lineHasContent = true;
        }

        void FlushToken()
        {
            WriteToken(token.ToString());
            token.Clear();
        }

        for (var index = 0; index < sql.Length; index++)
        {
            var character = sql[index];

            if (inSingleQuote)
            {
                result.Append(character);
                if (character == '\'' && (index + 1 >= sql.Length || sql[index + 1] != '\''))
                    inSingleQuote = false;
                else if (character == '\'' && index + 1 < sql.Length)
                    result.Append(sql[++index]);
                lineHasContent = true;
                continue;
            }

            if (inDoubleQuote)
            {
                result.Append(character);
                if (character == '"' && (index + 1 >= sql.Length || sql[index + 1] != '"'))
                    inDoubleQuote = false;
                else if (character == '"' && index + 1 < sql.Length)
                    result.Append(sql[++index]);
                lineHasContent = true;
                continue;
            }

            if (inBracketQuote)
            {
                result.Append(character);
                if (character == ']')
                    inBracketQuote = false;
                lineHasContent = true;
                continue;
            }

            if (afterWith && !char.IsWhiteSpace(character))
            {
                afterWith = false;
                if (character != '(')
                {
                    inCteList = true;
                    NewLine();
                }
            }

            if (character == '\'')
            {
                FlushToken();
                if (lineHasContent && result[^1] != ' ' && result[^1] != '(')
                    result.Append(' ');
                result.Append(character);
                inSingleQuote = true;
                lineHasContent = true;
            }
            else if (character == '"')
            {
                FlushToken();
                if (lineHasContent && result[^1] != ' ' && result[^1] != '.')
                    result.Append(' ');
                result.Append(character);
                inDoubleQuote = true;
                lineHasContent = true;
            }
            else if (character == '[')
            {
                FlushToken();
                if (lineHasContent && result[^1] != ' ' && result[^1] != '.')
                    result.Append(' ');
                result.Append(character);
                inBracketQuote = true;
                lineHasContent = true;
            }
            else if (char.IsWhiteSpace(character))
            {
                FlushToken();
                var commaBeforeWhitespace = result.Length > 0
                    && (result[^1] == ','
                        || result[^1] == ' '
                        && result.Length > 1
                        && result[^2] == ',');
                if (character == '\n' && commaBeforeWhitespace)
                    NewLine(1);
            }
            else if (character == '(')
            {
                FlushToken();
                afterWith = false;
                if (lineHasContent && result[^1] != ' ')
                    result.Append(' ');
                result.Append('(');
                lineHasContent = true;
                indent++;
            }
            else if (character == ')')
            {
                FlushToken();
                indent = Math.Max(0, indent - 1);
                result.Append(')');
                lineHasContent = true;
            }
            else if (character == ',')
            {
                FlushToken();
                result.Append(',');
                if (inCteList && indent == 0)
                    NewLine();
                else
                {
                    result.Append(' ');
                    lineHasContent = true;
                }
            }
            else
            {
                token.Append(character);
            }
        }

        FlushToken();
        return result.ToString().Trim();
    }
}
