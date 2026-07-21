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
            case "date": return FormatWithOffset(now, args, 0, 1, "yyyy-MM-dd", culture);
            case "time": return FormatWithOffset(now, args, 0, 1, "HH:mm:ss", culture);
            case "datetime": return FormatWithOffset(now, args, 0, 1, "yyyy-MM-dd HH:mm:ss", culture);
            case "iso": return now.ToString("yyyy-MM-ddTHH:mm:sszzz", culture);
            case "now": return now.ToString(args.Count > 0 ? args[0] : "yyyy-MM-dd HH:mm:ss", culture);
            case "utcnow": return now.ToUniversalTime().ToString(args.Count > 0 ? args[0] : "yyyy-MM-dd HH:mm:ss", culture);
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

    /// <summary>date/time/datetime/tomorrow/yesterday share this shape: an optional offset arg,
    /// an optional format arg (positions given by offsetIndex/formatIndex since tomorrow/yesterday
    /// have no offset arg of their own — their offset is fixed — but do take a format).</summary>
    private static string FormatWithOffset(
        DateTimeOffset baseTime, IReadOnlyList<string> args, int offsetIndex, int formatIndex, string defaultFormat, CultureInfo culture)
    {
        var offset = args.Count > offsetIndex ? args[offsetIndex] : null;
        var format = args.Count > formatIndex ? args[formatIndex] : defaultFormat;
        var adjusted = offsetIndex == formatIndex ? baseTime : ApplyOffset(baseTime, offset);
        return adjusted.ToString(format, culture);
    }

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
