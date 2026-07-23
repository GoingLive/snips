namespace Snips.Core.Templates;

/// <summary>One resolvable built-in variable's bare name and a short description, independent
/// of any example arguments — this is the MASTER KEY list translation names point back to (see
/// docs/language-pack-brief.md). Not the same list as the snippet editor's insertion helper,
/// which intentionally shows argument examples like "date:dd.MM.yyyy" rather than bare names.</summary>
public sealed record BuiltInVariableInfo(string Name, string Description);

/// <summary>
/// The authoritative list of every name BuiltInVariables.TryResolveAsync's switch statement
/// handles. Kept in sync by hand — there's no reflection-based generation, so a variable added
/// to BuiltInVariables.cs needs an entry here too, or it silently can't be translated.
/// </summary>
public static class BuiltInVariableCatalog
{
    public static readonly IReadOnlyList<BuiltInVariableInfo> All =
    [
        new("year", "Current year, 4 digits"),
        new("year2", "Current year, 2 digits"),
        new("month", "Current month, 2 digits"),
        new("day", "Current day, 2 digits"),
        new("hour", "Current hour, 12-hour clock"),
        new("minute", "Current minute"),
        new("second", "Current second"),
        new("date", "Today's date — optional offset and/or custom format"),
        new("time", "Current time — optional offset and/or custom format"),
        new("datetime", "Date and time together — optional offset and/or custom format"),
        new("iso", "ISO 8601 date/time with UTC offset"),
        new("localdate", "Short date in the Windows display-language format"),
        new("localtime", "Short time in the Windows display-language format"),
        new("locallongdate", "Long date in the Windows display-language format"),
        new("locallongtime", "Long time in the Windows display-language format"),
        new("intldate", "Spelled-out English date, regardless of locale"),
        new("now", "Current date/time — optional custom format"),
        new("utcnow", "Current UTC date/time — optional custom format"),
        new("timestamp", "Unix timestamp, seconds"),
        new("timestampms", "Unix timestamp, milliseconds"),
        new("weekday", "Day name, e.g. Tuesday"),
        new("monthname", "Month name, e.g. July"),
        new("week", "ISO-8601 week number"),
        new("quarter", "Current quarter, e.g. Q3"),
        new("dayofyear", "Day number within the year"),
        new("daysinmonth", "Number of days in the current month"),
        new("tomorrow", "Tomorrow's date — optional custom format"),
        new("yesterday", "Yesterday's date — optional custom format"),
        new("timezone", "Windows time zone ID"),
        new("utcoffset", "Current UTC offset, e.g. +02:00"),
        new("snipsversion", "The build of Snips currently running"),
        new("user", "Windows login name"),
        new("userfullname", "Windows full display name"),
        new("useremail", "Email address set in Settings"),
        new("machine", "Computer name"),
        new("domain", "Windows domain or workgroup"),
        new("os", "Operating system name"),
        new("osversion", "Operating system version"),
        new("ip", "Local IP address"),
        new("home", "User home folder path"),
        new("desktop", "Desktop folder path"),
        new("documents", "Documents folder path"),
        new("downloads", "Downloads folder path"),
        new("temp", "Temp folder path"),
        new("appdata", "AppData folder path"),
        new("clipboard", "Current clipboard text"),
        new("selection", "Not yet implemented — always empty"),
        new("activewindow", "Title of the window Snips will paste into"),
        new("activeapp", "Name of the app Snips will paste into"),
        new("snippetname", "This snippet's own name"),
        new("snippetid", "This snippet's internal ID"),
        new("snippetdescription", "This snippet's own description"),
        new("usecount", "Times this snippet has been used"),
        new("guid", "A random unique ID"),
        new("id", "A locally-unique, sortable ID (Snowflake)"),
        new("random", "A random number in a range"),
        new("randomstring", "A random alphanumeric string"),
        new("counter", "A persistent counter, increments each use"),
    ];
}
