using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Snips.Core.Templates;

/// <summary>Chainable value filters per SPEC.md §7.7. An unknown filter name or a bad argument
/// (non-numeric N, invalid base64, ...) leaves the value unchanged rather than failing the
/// paste — consistent with the "degrade, don't fail" principle (SPEC.md §1.1).</summary>
public static class TemplateFilters
{
    public static string Apply(string filterName, string input, IReadOnlyList<string> args) => filterName.ToLowerInvariant() switch
    {
        "upper" => input.ToUpperInvariant(),
        "lower" => input.ToLowerInvariant(),
        "title" => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(input.ToLowerInvariant()),
        "capitalize" => Capitalize(input),
        "trim" => input.Trim(),
        "slug" => Slugify(input),
        "urlencode" => Uri.EscapeDataString(input),
        "urldecode" => TryUnescape(input),
        "base64" => Convert.ToBase64String(Encoding.UTF8.GetBytes(input)),
        "unbase64" => TryFromBase64(input),
        "md5" => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant(),
        "sha256" => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant(),
        "reverse" => new string(input.Reverse().ToArray()),
        "left" => Left(input, args),
        "right" => Right(input, args),
        "replace" => Replace(input, args),
        "padleft" => Pad(input, args, padRight: false),
        "padright" => Pad(input, args, padRight: true),
        "escapexml" => EscapeXml(input),
        "escapejson" => EscapeJson(input),
        "nospaces" => new string(input.Where(c => !char.IsWhiteSpace(c)).ToArray()),
        _ => input,
    };

    private static string Capitalize(string s) =>
        s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static string Slugify(string s)
    {
        var lowered = s.ToLowerInvariant();
        var builder = new StringBuilder();
        var lastWasDash = false;

        foreach (var c in lowered)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
                lastWasDash = false;
            }
            else if (!lastWasDash && builder.Length > 0)
            {
                builder.Append('-');
                lastWasDash = true;
            }
        }

        return builder.ToString().TrimEnd('-');
    }

    private static string TryUnescape(string s)
    {
        try { return Uri.UnescapeDataString(s); }
        catch (Exception) { return s; }
    }

    private static string TryFromBase64(string s)
    {
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); }
        catch (FormatException) { return s; }
    }

    private static string Left(string s, IReadOnlyList<string> args) =>
        args.Count > 0 && int.TryParse(args[0], out var n) && n >= 0 && n < s.Length ? s[..n] : s;

    private static string Right(string s, IReadOnlyList<string> args) =>
        args.Count > 0 && int.TryParse(args[0], out var n) && n >= 0 && n < s.Length ? s[^n..] : s;

    private static string Replace(string s, IReadOnlyList<string> args)
    {
        if (args.Count == 0)
            return s;

        var parts = args[0].Split(',', 2);
        var from = parts[0];
        var to = parts.Length > 1 ? parts[1] : string.Empty;
        return from.Length == 0 ? s : s.Replace(from, to);
    }

    private static string Pad(string s, IReadOnlyList<string> args, bool padRight)
    {
        // width < 0 isn't just "unusual," it's a hard throw from PadLeft/PadRight
        // (ArgumentOutOfRangeException) — this file's own stated rule is that a bad argument
        // leaves the value unchanged, not that it crashes the caller.
        if (args.Count == 0 || !int.TryParse(args[0].Split(',')[0], out var width) || width < 0)
            return s;

        var parts = args[0].Split(',', 2);
        var padChar = parts.Length > 1 && parts[1].Length > 0 ? parts[1][0] : ' ';

        return padRight ? s.PadRight(width, padChar) : s.PadLeft(width, padChar);
    }

    private static string EscapeXml(string s) => s
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;")
        .Replace("'", "&apos;");

    private static string EscapeJson(string s)
    {
        // Hand-rolled rather than JsonSerializer.Serialize: its default encoder emits "
        // for a quote instead of the conventional \", which would surprise anyone pasting
        // this into hand-written JSON.
        var builder = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            switch (c)
            {
                case '\\': builder.Append("\\\\"); break;
                case '"': builder.Append("\\\""); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (c < 0x20)
                        builder.Append("\\u").Append(((int)c).ToString("x4"));
                    else
                        builder.Append(c);
                    break;
            }
        }
        return builder.ToString();
    }
}
