using System.Globalization;
using System.Text.RegularExpressions;

namespace Snips.Core.Templates;

/// <summary>
/// Resolves every non-interactive built-in variable from SPEC.md §7.1–§7.5. Interactive
/// variables (§7.6) are resolved separately by TemplateEngine using pre-collected prompt
/// answers, since they need one shared form rather than a per-placeholder popup.
/// Returns null for a name this resolver doesn't own, so the engine can decide the fallback
/// (currently: leave the placeholder text unchanged — SPEC.md §1.1 "degrade, don't fail").
/// </summary>
internal static partial class BuiltInVariables
{
    public static async ValueTask<string?> TryResolveAsync(
        string name, IReadOnlyList<string> args, TemplateContext context, CancellationToken ct)
    {
        var culture = context.Culture;
        var now = context.Now;

        switch (name.ToLowerInvariant())
        {
            // §7.1 Date and time
            case "year": return now.ToString("yyyy", culture);
            case "year2": return now.ToString("yy", culture);
            case "month": return now.ToString("MM", culture);
            case "day": return now.ToString("dd", culture);
            case "hour": return now.ToString("hh", culture);
            case "minute": return now.ToString("mm", culture);
            case "second": return now.ToString("ss", culture);
            case "date": return FormatDateOrTime(now, args, "yyyy-MM-dd", culture);
            case "time": return FormatDateOrTime(now, args, "HH:mm:ss", culture);
            case "datetime": return FormatDateOrTime(now, args, "yyyy-MM-dd HH:mm:ss", culture);
            case "iso": return now.ToString("yyyy-MM-ddTHH:mm:sszzz", culture);
            case "localdate": return now.ToString(culture.DateTimeFormat.ShortDatePattern, culture);
            case "localtime": return now.ToString(culture.DateTimeFormat.ShortTimePattern, culture);
            case "locallongdate": return now.ToString(culture.DateTimeFormat.LongDatePattern, culture);
            case "locallongtime": return now.ToString(culture.DateTimeFormat.LongTimePattern, culture);
            // Spelled-out month, culture-independent (e.g. "10 April 2026") regardless of the
            // system locale — for correspondence meant to read unambiguously in English no
            // matter who opens it, as opposed to {{localdate}} which follows the user's locale.
            case "intldate": return now.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("en-US"));
            case "now": return now.ToString(args.Count > 0 ? RejoinFormat(args) : "yyyy-MM-dd HH:mm:ss", culture);
            case "utcnow": return now.ToUniversalTime().ToString(args.Count > 0 ? RejoinFormat(args) : "yyyy-MM-dd HH:mm:ss", culture);
            case "timestamp": return now.ToUnixTimeSeconds().ToString(culture);
            case "timestampms": return now.ToUnixTimeMilliseconds().ToString(culture);
            case "weekday": return now.ToString("dddd", culture);
            case "monthname": return now.ToString("MMMM", culture);
            case "week": return ISOWeek.GetWeekOfYear(now.DateTime).ToString(culture);
            case "quarter": return $"Q{(now.Month - 1) / 3 + 1}";
            case "dayofyear": return now.DayOfYear.ToString(culture);
            case "daysinmonth": return DateTime.DaysInMonth(now.Year, now.Month).ToString(culture);
            case "tomorrow": return FormatWithOffset(ApplyOffset(now, "+1d"), args, 0, 0, "yyyy-MM-dd", culture);
            case "yesterday": return FormatWithOffset(ApplyOffset(now, "-1d"), args, 0, 0, "yyyy-MM-dd", culture);
            case "timezone": return TimeZoneInfo.Local.Id;
            case "utcoffset": return now.ToString("zzz", culture);

            // §7.2 Identity, system, and paths
            case "snipsversion": return context.AppVersion ?? string.Empty;
            case "user": return context.SystemInfo.UserName;
            case "userfullname": return context.SystemInfo.UserFullName;
            case "useremail": return context.UserEmail ?? string.Empty;
            case "machine": return context.SystemInfo.MachineName;
            case "domain": return context.SystemInfo.DomainName;
            case "os": return context.SystemInfo.OsName;
            case "osversion": return context.SystemInfo.OsVersion;
            case "ip": return context.SystemInfo.IpAddress;
            case "home": return context.SystemInfo.HomeDirectory;
            case "desktop": return context.SystemInfo.DesktopDirectory;
            case "documents": return context.SystemInfo.DocumentsDirectory;
            case "downloads": return context.SystemInfo.DownloadsDirectory;
            case "temp": return context.SystemInfo.TempDirectory;
            case "appdata": return context.SystemInfo.AppDataDirectory;

            // §7.3 Context (selection deferred — see TemplateContext.ClipboardText doc comment)
            case "clipboard": return context.ClipboardText ?? string.Empty;
            case "selection": return string.Empty;
            case "activewindow": return context.ActiveWindowTitle ?? string.Empty;
            case "activeapp": return context.ActiveAppName ?? string.Empty;

            // §7.4 Snippet metadata
            case "snippetname": return context.SnippetName;
            case "snippetid": return context.SnippetId;
            case "snippetdescription": return context.SnippetDescription;
            case "usecount": return context.UseCount.ToString(culture);

            // §7.5 Generators
            case "guid": return Guid.NewGuid().ToString();
            case "id": return context.IdGenerator?.NextId() ?? string.Empty;
            case "random": return RandomInRange(args, 0, 99).ToString(culture);
            case "randomstring": return RandomString(args.Count > 0 && int.TryParse(args[0], out var len) ? len : 12);
            case "counter": return await ResolveCounterAsync(args, context, ct);

            default: return null;
        }
    }

    /// <summary>tomorrow/yesterday take a single optional format arg — their offset is fixed,
    /// not user-supplied, so there's no offset-vs-format ambiguity to resolve. The format still
    /// needs RejoinFormat: a format like "HH:mm" arrives from the parser already shredded into
    /// separate args by its own colon (see RejoinFormat's doc comment).</summary>
    private static string FormatWithOffset(
        DateTimeOffset baseTime, IReadOnlyList<string> args, int offsetIndex, int formatIndex, string defaultFormat, CultureInfo culture)
    {
        var format = args.Count > formatIndex ? RejoinFormat(args) : defaultFormat;
        return baseTime.ToString(format, culture);
    }

    /// <summary>date/time/datetime take an optional offset AND an optional format, in either
    /// order of presence: {{date}}, {{date:dd.MM.yyyy}} (format only — by far the common case),
    /// {{date:+7d}} (offset only), or {{date:+7d:dd.MM.yyyy}} (both). A single arg is ambiguous
    /// between "offset" and "format" by position alone, so it's classified by shape instead: it's
    /// an offset only if it matches the offset grammar (e.g. "+7d"), otherwise it's a format.
    /// Previously a single format-only arg was always misread as a (non-matching, silently
    /// ignored) offset, so {{date:dd.MM.yyyy}} rendered as the untouched default format instead
    /// of the requested one.</summary>
    private static string FormatDateOrTime(DateTimeOffset now, IReadOnlyList<string> args, string defaultFormat, CultureInfo culture)
    {
        if (args.Count == 0)
            return now.ToString(defaultFormat, culture);

        // Classify by the FIRST arg's shape, not by how many pieces the parser handed back —
        // a colon inside the format (e.g. "HH:mm") makes args.Count vary independently of
        // whether an offset was actually given, so counting args can't tell offset-only from
        // format-only. Only args[0] can possibly be an offset; if it isn't, every arg belongs
        // to the format (see RejoinFormat).
        if (OffsetPattern().IsMatch(args[0]))
        {
            var format = args.Count > 1 ? RejoinFormat(args.Skip(1).ToList()) : defaultFormat;
            return ApplyOffset(now, args[0]).ToString(format, culture);
        }

        return now.ToString(RejoinFormat(args), culture);
    }

    /// <summary>A format string containing its own ':' (e.g. "HH:mm", extremely common for
    /// time) gets split apart by TemplateParser before a variable ever sees it, since ':' is
    /// also the placeholder's own argument separator — {{now:HH:mm:ss}} arrives here as three
    /// separate args ["HH","mm","ss"], not one. There's no way to tell the parser apart from
    /// here, so instead of asking users to avoid ':' in formats (unworkable — it's the standard
    /// time separator), every remaining positional arg after the offset is rejoined with ':'
    /// to reconstruct the original format string.</summary>
    private static string RejoinFormat(IReadOnlyList<string> args) => string.Join(':', args);

    [GeneratedRegex(@"^([+-]?\d+)(min|s|h|d|w|m|y)$", RegexOptions.IgnoreCase)]
    private static partial Regex OffsetPattern();

    internal static DateTimeOffset ApplyOffset(DateTimeOffset baseTime, string? offsetArg)
    {
        if (string.IsNullOrWhiteSpace(offsetArg))
            return baseTime;

        var match = OffsetPattern().Match(offsetArg);
        if (!match.Success)
            return baseTime;

        var amount = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);

        return match.Groups[2].Value.ToLowerInvariant() switch
        {
            "s" => baseTime.AddSeconds(amount),
            "min" => baseTime.AddMinutes(amount),
            "h" => baseTime.AddHours(amount),
            "d" => baseTime.AddDays(amount),
            "w" => baseTime.AddDays(amount * 7),
            "m" => baseTime.AddMonths(amount),
            "y" => baseTime.AddYears(amount),
            _ => baseTime,
        };
    }

    private static int RandomInRange(IReadOnlyList<string> args, int defaultMin, int defaultMax)
    {
        var (min, max) = (defaultMin, defaultMax);

        if (args.Count > 0)
        {
            var parts = args[0].Split('-', 2);
            if (parts.Length == 2 && int.TryParse(parts[0], out var lo) && int.TryParse(parts[1], out var hi))
                (min, max) = (lo, hi);
        }

        return Random.Shared.Next(min, max + 1);
    }

    private const string RandomStringAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    private static string RandomString(int length) =>
        new(Enumerable.Range(0, Math.Max(0, length)).Select(_ => RandomStringAlphabet[Random.Shared.Next(RandomStringAlphabet.Length)]).ToArray());

    private static async ValueTask<string> ResolveCounterAsync(IReadOnlyList<string> args, TemplateContext context, CancellationToken ct)
    {
        if (args.Count == 0 || context.Counters is null)
            return string.Empty;

        var name = args[0];
        var step = args.Count > 1 && long.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) ? s : 1;
        var format = args.Count > 2 ? args[2] : null;

        var value = await context.Counters.IncrementAndGetAsync(name, step, ct);
        return format is null ? value.ToString(CultureInfo.InvariantCulture) : value.ToString(format, CultureInfo.InvariantCulture);
    }
}
