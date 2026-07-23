using System.Globalization;
using Snips.Core.Id;

namespace Snips.Core.Templates;

/// <summary>The render-time environment for a single snippet application. See SPEC.md §7 and §8.4.</summary>
public sealed class TemplateContext
{
    public required DateTimeOffset Now { get; init; }
    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;

    // Snippet metadata — §7.4
    public string SnippetName { get; init; } = string.Empty;
    public string SnippetId { get; init; } = string.Empty;
    public string SnippetDescription { get; init; } = string.Empty;
    public int UseCount { get; init; }

    // Identity/system — §7.2
    public required ISystemInfoProvider SystemInfo { get; init; }
    public string? UserEmail { get; init; }

    /// <summary>Backs {{snipsversion}}. Set from Snips.App.BuildIdentifier — Core can't
    /// reference App directly, so the caller supplies the already-computed string.</summary>
    public string? AppVersion { get; init; }

    // Context — §7.3. {{selection}} is intentionally not here yet; it needs to be captured
    // by simulating Ctrl+C against the paste target before rendering even starts (§6.3),
    // which is a separate, best-effort-by-design piece of work from template rendering itself.
    public string? ClipboardText { get; init; }
    public string? ActiveWindowTitle { get; init; }
    public string? ActiveAppName { get; init; }

    // Generators — §7.5
    public SnowflakeIdGenerator? IdGenerator { get; init; }
    public ICounterStore? Counters { get; init; }

    // Interactive prompts — §7.6
    public IInteractivePrompt? Prompt { get; init; }

    /// <summary>Loaded fresh by the caller from external-variables.json, if present — see
    /// ExternalVariablesLoader and docs/variables.yaml. Checked as a fallback tier, after
    /// built-ins, before a placeholder is left as literal text.</summary>
    public IReadOnlyDictionary<string, string>? ExternalVariables { get; init; }

    /// <summary>LocalName -> MasterKey, for the current language — see
    /// docs/language-pack-brief.md. A name unresolved as a built-in is checked here for a
    /// translated alias (e.g. German "heute" -> "date") and re-resolved as that master key
    /// before falling through to ExternalVariables and then literal text. Always empty/null for
    /// English, since English names ARE the master keys.</summary>
    public IReadOnlyDictionary<string, string>? VariableNameTranslations { get; init; }
}
