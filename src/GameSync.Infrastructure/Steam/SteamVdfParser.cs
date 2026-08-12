namespace GameSync.Infrastructure.Steam;

/// <summary>
/// Minimal parser for Valve KeyValues format (libraryfolders.vdf / appmanifest ACF).
/// Handles only the flat key-value pairs and simple nested sections needed for Steam library discovery.
/// </summary>
public static class SteamVdfParser
{
    public static Dictionary<string, string> ParseFlat(string content)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(content))
        {
            return result;
        }

        var span = content.AsSpan();
        int pos = 0;
        int depth = 0;

        while (pos < span.Length)
        {
            SkipWhitespace(span, ref pos);
            if (pos >= span.Length) break;

            if (span[pos] == '{')
            {
                depth++;
                pos++;
                continue;
            }

            if (span[pos] == '}')
            {
                depth--;
                pos++;
                continue;
            }

            var key = ReadQuotedString(span, ref pos);
            if (key is null) break;

            SkipWhitespace(span, ref pos);
            if (pos >= span.Length) break;

            if (span[pos] == '{')
            {
                if (depth >= 1)
                {
                    depth++;
                    pos++;
                    SkipBlock(span, ref pos);
                    depth--;
                }
                else
                {
                    depth++;
                    pos++;
                }
                continue;
            }

            var value = ReadQuotedString(span, ref pos);
            if (value is not null && depth == 1)
            {
                result[key] = value;
            }
        }

        return result;
    }

    public static IReadOnlyList<string> ParseLibraryFolderPaths(string content)
    {
        var paths = new List<string>();
        if (string.IsNullOrWhiteSpace(content))
        {
            return paths;
        }

        var span = content.AsSpan();
        int pos = 0;
        int depth = 0;

        while (pos < span.Length)
        {
            SkipWhitespace(span, ref pos);
            if (pos >= span.Length) break;

            if (span[pos] == '{')
            {
                depth++;
                pos++;
                continue;
            }

            if (span[pos] == '}')
            {
                depth--;
                pos++;
                continue;
            }

            var key = ReadQuotedString(span, ref pos);
            if (key is null) break;

            SkipWhitespace(span, ref pos);
            if (pos >= span.Length) break;

            if (span[pos] == '{')
            {
                if (depth == 0)
                {
                    depth++;
                    pos++;
                }
                else if (depth == 1)
                {
                    pos++;
                    depth++;
                    var nested = ParseNestedBlock(span, ref pos);
                    depth--;
                    if (nested.TryGetValue("path", out var path) && !string.IsNullOrWhiteSpace(path))
                    {
                        paths.Add(path.Replace("\\\\", "\\"));
                    }
                }
                else
                {
                    depth++;
                    pos++;
                    SkipBlock(span, ref pos);
                    depth--;
                }
            }
            else
            {
                ReadQuotedString(span, ref pos);
            }
        }

        return paths;
    }

    private static Dictionary<string, string> ParseNestedBlock(ReadOnlySpan<char> span, ref int pos)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int depth = 1;
        while (pos < span.Length && depth > 0)
        {
            SkipWhitespace(span, ref pos);
            if (pos >= span.Length) break;

            if (span[pos] == '}')
            {
                depth--;
                pos++;
                continue;
            }

            if (span[pos] == '{')
            {
                depth++;
                pos++;
                continue;
            }

            var key = ReadQuotedString(span, ref pos);
            if (key is null) break;

            SkipWhitespace(span, ref pos);
            if (pos >= span.Length) break;

            if (span[pos] == '{')
            {
                depth++;
                pos++;
                continue;
            }

            var value = ReadQuotedString(span, ref pos);
            if (value is not null && depth == 1)
            {
                result[key] = value;
            }
        }

        return result;
    }

    private static string? ReadQuotedString(ReadOnlySpan<char> span, ref int pos)
    {
        if (pos >= span.Length || span[pos] != '"')
        {
            return null;
        }

        pos++;
        var start = pos;
        while (pos < span.Length)
        {
            if (span[pos] == '\\' && pos + 1 < span.Length)
            {
                pos += 2;
                continue;
            }

            if (span[pos] == '"')
            {
                var value = span[start..pos].ToString().Replace("\\\\", "\\");
                pos++;
                return value;
            }

            pos++;
        }

        return span[start..].ToString();
    }

    private static void SkipWhitespace(ReadOnlySpan<char> span, ref int pos)
    {
        while (pos < span.Length && char.IsWhiteSpace(span[pos]))
        {
            pos++;
        }
    }

    private static void SkipBlock(ReadOnlySpan<char> span, ref int pos)
    {
        int depth = 1;
        while (pos < span.Length && depth > 0)
        {
            if (span[pos] == '{') depth++;
            else if (span[pos] == '}') depth--;
            pos++;
        }
    }
}
