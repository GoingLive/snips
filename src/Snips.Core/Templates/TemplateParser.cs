using System.Text;

namespace Snips.Core.Templates;

/// <summary>
/// Parses `{{name}}`, `{{name:arg1:arg2}}`, `{{name|filter1|filter2:arg}}` per SPEC.md §7.
/// `:` always separates a variable's own positional arguments; `|` is reserved for the
/// trailing filter chain (see §7.6's grammar-fix note for why these can't share a separator).
/// A literal `{{` is written `\{{`. An unterminated `{{` (no matching `}}`) is treated as
/// literal text rather than an error — malformed input should never block a paste.
/// </summary>
public static class TemplateParser
{
    public static IReadOnlyList<TemplateSegment> Parse(string source)
    {
        var segments = new List<TemplateSegment>();
        var literal = new StringBuilder();
        var i = 0;

        while (i < source.Length)
        {
            if (source[i] == '\\' && i + 3 <= source.Length && source[i + 1] == '{' && source[i + 2] == '{')
            {
                literal.Append("{{");
                i += 3;
                continue;
            }

            if (i + 1 < source.Length && source[i] == '{' && source[i + 1] == '{')
            {
                var close = source.IndexOf("}}", i + 2, StringComparison.Ordinal);
                if (close < 0)
                {
                    literal.Append(source, i, source.Length - i);
                    break;
                }

                if (literal.Length > 0)
                {
                    segments.Add(new LiteralSegment(literal.ToString()));
                    literal.Clear();
                }

                var inner = source.Substring(i + 2, close - (i + 2));
                segments.Add(ParsePlaceholder(inner));
                i = close + 2;
                continue;
            }

            literal.Append(source[i]);
            i++;
        }

        if (literal.Length > 0)
            segments.Add(new LiteralSegment(literal.ToString()));

        return segments;
    }

    private static PlaceholderSegment ParsePlaceholder(string inner)
    {
        var pipeParts = inner.Split('|');
        var mainParts = pipeParts[0].Split(':');

        var name = mainParts[0].Trim();
        var args = mainParts.Skip(1).ToList();
        var filters = pipeParts.Skip(1).Select(ParseFilter).ToList();

        return new PlaceholderSegment(name, args, filters);
    }

    private static FilterSpec ParseFilter(string spec)
    {
        var parts = spec.Split(':');
        return new FilterSpec(parts[0].Trim(), parts.Skip(1).ToList());
    }
}
