using System.Text;

namespace XianYuLauncher.Core.Helpers;

public readonly record struct AiContextTextSlice(
    int StartOffset,
    int EndOffset,
    int TotalLength,
    bool WasTruncated,
    string Content);

public static class AiContextFormattingHelper
{
    public static string RemoveClassPathArguments(string launchCommand, out bool classPathRemoved)
    {
        if (string.IsNullOrWhiteSpace(launchCommand))
        {
            classPathRemoved = false;
            return launchCommand;
        }

        var tokens = TokenizeCommandLine(launchCommand);
        if (tokens.Count == 0)
        {
            classPathRemoved = false;
            return launchCommand;
        }

        classPathRemoved = false;
        List<string> filteredTokens = [];

        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (!IsClassPathSwitch(token))
            {
                filteredTokens.Add(token);
                continue;
            }

            classPathRemoved = true;
            if (!HasInlineClassPathValue(token) && index + 1 < tokens.Count)
            {
                index++;
            }
        }

        return JoinCommandLineTokens(filteredTokens);
    }

    public static string? TryGetJavaExecutable(string launchCommand)
    {
        var tokens = TokenizeCommandLine(launchCommand);
        return tokens.Count > 0 ? tokens[0] : null;
    }

    public static AiContextTextSlice GetTailSlice(string content, int maxChars)
    {
        ValidateMaxChars(maxChars);
        content ??= string.Empty;

        if (content.Length <= maxChars)
        {
            return new AiContextTextSlice(0, content.Length, content.Length, false, content);
        }

        var startOffset = content.Length - maxChars;
        return new AiContextTextSlice(
            startOffset,
            content.Length,
            content.Length,
            true,
            content[startOffset..]);
    }

    public static AiContextTextSlice GetSlice(string content, int startOffset, int maxChars)
    {
        if (startOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startOffset));
        }

        ValidateMaxChars(maxChars);
        content ??= string.Empty;

        if (startOffset >= content.Length)
        {
            return new AiContextTextSlice(content.Length, content.Length, content.Length, false, string.Empty);
        }

        var length = Math.Min(maxChars, content.Length - startOffset);
        var endOffset = startOffset + length;
        var wasTruncated = startOffset > 0 || endOffset < content.Length;

        return new AiContextTextSlice(
            startOffset,
            endOffset,
            content.Length,
            wasTruncated,
            content.Substring(startOffset, length));
    }

    private static void ValidateMaxChars(int maxChars)
    {
        if (maxChars <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxChars));
        }
    }

    private static bool IsClassPathSwitch(string token)
    {
        return token.Equals("-cp", StringComparison.OrdinalIgnoreCase)
            || token.Equals("-classpath", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("-cp=", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("-classpath=", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasInlineClassPathValue(string token)
    {
        return token.StartsWith("-cp=", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("-classpath=", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> TokenizeCommandLine(string commandLine)
    {
        List<string> tokens = [];
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return tokens;
        }

        StringBuilder currentToken = new();
        bool inQuotes = false;

        foreach (var character in commandLine)
        {
            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && char.IsWhiteSpace(character))
            {
                if (currentToken.Length > 0)
                {
                    tokens.Add(currentToken.ToString());
                    currentToken.Clear();
                }

                continue;
            }

            currentToken.Append(character);
        }

        if (currentToken.Length > 0)
        {
            tokens.Add(currentToken.ToString());
        }

        return tokens;
    }

    private static string JoinCommandLineTokens(IEnumerable<string> tokens)
    {
        return string.Join(" ", tokens.Select(QuoteTokenIfNeeded));
    }

    private static string QuoteTokenIfNeeded(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return "\"\"";
        }

        if (!token.Any(char.IsWhiteSpace) && !token.Contains('"'))
        {
            return token;
        }

        return $"\"{token.Replace("\"", "\\\"")}\"";
    }
}