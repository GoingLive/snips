# Snips — Specification

**Version:** 0.1 (draft)
**Date:** 2026-07-21
**Author:** Roland
**Repository:** https://github.com/GoingLive/snips
**Status:** Draft for review — open questions marked `[?]`

---

## 1. Summary

Snips is a small, always-available Windows desktop application that stores reusable
rich-text snippets and pastes them into whatever application the user is currently
working in. Snippets may contain placeholders (`{{year}}`, `{{clipboard}}`,
`{{input:Customer}}`) that are resolved at the moment of use.

The design goal is **zero-friction recall**: hit a hotkey, type three letters, press
Enter, and the text is in the target application. Everything else — editing,
variables, scripting — is secondary to that path staying fast.

### 1.1 Design principles

1. **The fast path is sacred.** Hotkey → filter → Enter must never be blocked by a
   dialog, an update check, a donation prompt, or a slow script.
2. **Never lose user data.** A snippet library represents accumulated effort. Backups,
   exports, and migration safety take priority over features.
3. **Degrade, don't fail.** If a variable can't resolve, if the target app rejects
   rich text, if a hotkey is taken — insert what we can, tell the user plainly,
   and keep going.
4. **No accounts, no server, no telemetry.** Snips is a local tool. It does not phone
   home. The only network call is an optional, user-disableable update check.

---

## 2. Scope

### 2.1 In scope for v1.0

- Rich text snippets (formatting + embedded images) stored in SQLite
- Built-in variables (date/time, system, context, generators, interactive prompts)
- User-defined variables via expressions, Lua, or JavaScript
- Per-snippet global hotkeys plus one global "open picker" hotkey
- Paste into the previously focused application; and/or copy to clipboard
- Fuzzy incremental search over the snippet library
- Portable single-file executable, no installer, no admin rights

### 2.2 Explicitly out of scope for v1.0

| Not in v1 | Reason | Revisit |
|---|---|---|
| Cross-platform (macOS/Linux) | Confirmed Windows-only | Never |
| Cloud sync / team sharing | Requires a server and an account system | Phase 4 |
| Abbreviation auto-expansion (type `;addr` → expands) | Requires a low-level keyboard hook; see §12 risk R2 | Phase 3 |
| File / network / process access from user scripts | Sandbox escape surface | Phase 3, opt-in per variable |
| Licence server, subscriptions, paid tiers | See §11 | Only if the project outgrows donations |

---

## 3. Technology decisions

| Concern | Decision | Rationale |
|---|---|---|
| Runtime | .NET 10 (`net10.0-windows`) | Installed (SDK 10.0.302); current LTS-track |
| Application shell | **WPF** | Window, picker list, search, settings. Native, instant startup, trivial `Topmost` and custom chrome. The fast path never touches a browser engine. |
| Snippet body format | **HTML + inline CSS** | See §3.3. Universally understood, diffable, future-proof, and it *is* the clipboard's rich format on Windows. |
| Body editor + preview | **WebView2** (`contenteditable`) | The only way to edit HTML without a lossy conversion layer. Runtime confirmed present on this machine (v150.0.4078.83) and ships with Windows 11. |
| Database | SQLite via `Microsoft.Data.Sqlite` | Single file, zero-config, ships in-process. No ORM — hand-written SQL in a thin repository layer keeps the binary small and the queries obvious. |
| Expression engine | `NCalc` (or `DynamicExpresso`) | Math/logic one-liners, ~200 KB |
| Lua engine | `MoonSharp` | Pure managed, sandboxable, no native dependency |
| JavaScript engine | `Jint` | Pure managed, interpreter with time/memory limits |
| Tray icon | `H.NotifyIcon.Wpf` | Maintained; avoids WinForms interop |
| Tests | xUnit + FluentAssertions | — |
| Packaging | Single-file, self-contained, win-x64, ReadyToRun | No installer, no runtime prerequisite |

### 3.1 On xTalk / HyperTalk

Roland raised xTalk (HyperCard → LiveCode) as a preferred, English-readable syntax.
**There is no maintained embeddable xTalk engine for .NET**, so it cannot be a v1
scripting backend. Two things follow from the preference, though:

1. The built-in variable syntax (§7) is deliberately English-readable and
   argument-driven, so that the large majority of real snippets need **no scripting
   at all** — `{{date:+7d:dd.MM.yyyy}}` rather than a script.
2. Script engines sit behind an `IScriptEngine` interface (§8.3). An xTalk-flavoured
   dialect could later be implemented as a fourth engine without touching anything
   else.

### 3.2 Solution layout

```
snips/
├─ src/
│  ├─ Snips.App/          WPF application — views, view models, composition root
│  ├─ Snips.Core/         Domain model, template engine, scripting, ID generation
│  ├─ Snips.Data/         SQLite repositories, schema, migrations, backup
│  └─ Snips.Interop/      Win32 P/Invoke — hotkeys, foreground window, SendInput,
│                         clipboard (CF_HTML wrapping)
├─ tests/
│  └─ Snips.Tests/
├─ docs/
│  └─ SPEC.md
├─ assets/               Icon sources
├─ LICENSE
└─ README.md
```

`Snips.Core` and `Snips.Data` have no WPF dependency and are fully unit-testable.
`Snips.Interop` is the only project containing `DllImport`.

### 3.3 HTML rather than RTF — and what "deprecated" actually means

RTF is genuinely old (1987) and Microsoft stopped publishing new versions of the
specification in 2008. But it was never withdrawn, and it is still one of the two
rich-text formats the Windows clipboard actually carries — Word and Outlook both
publish and consume it today. So "no longer supported" is not quite right: RTF is
*frozen*, not dead.

The right conclusion is still to build on HTML, for reasons that have little to do
with RTF's age:

| | HTML + CSS | RTF |
|---|---|---|
| Editing | `contenteditable` in WebView2 gives a real editor for free | Requires WPF `FlowDocument`, then a hand-written serialiser |
| Images | `<img src="data:image/png;base64,…">` | `{\pict\pngblip}` hex blobs |
| Clipboard | `CF_HTML` — accepted by Word, Outlook, browsers, Gmail, Slack, Teams, OneNote, Notion | `CF_RTF` — accepted by Word, Outlook, WordPad, LibreOffice |
| Storage | Text. Diffable, greppable, hand-editable, Git-friendly | Opaque |
| Variable substitution | Walk DOM text nodes | Parse a bespoke control-word grammar |
| Future formats | Markdown, PDF, plain text all convert cleanly from HTML | Everything is a conversion |

Decisively: the previous draft required a hand-written `FlowDocument` → RTF writer
with embedded-image support, because WPF's own RTF writer silently drops images.
That was the single largest risk in the project. **Choosing HTML deletes that work
entirely** — the canonical stored form and the primary clipboard form become the
same string, so there is no serialiser to get wrong.

**Storage format:** HTML fragments with *inline* styles only (`<span style="…">`),
no stylesheets, no scripts, no external references. Sanitised on save with a strict
allow-list of tags and attributes, so a snippet pasted in from a web page cannot
carry scripts, tracking pixels, or remote images into the database.

**RTF is not abandoned, just demoted.** `CF_RTF` remains a Phase-6 nice-to-have,
generated from the HTML by a converter if the paste-target matrix (§13.2) shows any
target that needs it. Every target on that list accepts `CF_HTML`, so the
expectation is that it will not be needed.

**Cost of WebView2:** it is a runtime dependency rather than something bundled — but
it ships in Windows 11 and is present on effectively all Windows 10 machines via
Edge. It is confirmed installed here. The editor WebView2 instance is created
lazily on first use and kept warm; the picker's fast path (§5.4) is pure WPF and
never waits for it.

---

## 4. Data model

### 4.1 Identifiers

Every snippet, variable, and asset gets a **snowflake ID**: a 63-bit monotonically
increasing integer composed of

```
[ 41 bits: milliseconds since epoch 2024-01-01T00:00:00Z ]
[ 10 bits: instance id (random per installation, stored in settings) ]
[ 12 bits: per-millisecond sequence ]
```

**Deviation from the brief, please confirm `[?]`:** the brief asked for exactly 18
digits. A snowflake on this epoch yields 18 digits today and rolls over to 19 digits
around 2031. Rather than cap it, IDs are stored as **TEXT zero-padded to 19
characters**, so that lexicographic ordering in SQLite equals numeric ordering
forever. Displayed to the user without padding.

### 4.2 Rich text storage

- **Canonical storage** is an **HTML fragment with inline CSS**, stored as TEXT
  (§3.3). This is what the editor loads, what the database holds, and what goes onto
  the clipboard as `CF_HTML` — one representation, no conversion step.
- **`PlainText`** is a derived column, regenerated on every save from the HTML, used
  for search and for the plain-text clipboard format.
- **Sanitisation on save** enforces an allow-list: `p b i u s em strong span div br
  ul ol li a img h1–h6 blockquote code pre table tr td th hr` and the attributes
  `style href src alt width height`. `style` is filtered to a safe property set.
  Everything else is stripped. No `<script>`, no `<link>`, no event handlers, no
  remote URLs.

**Images** are stored as `SnippetAsset` rows, de-duplicated by SHA-256 content hash,
and referenced from the HTML by a custom scheme — `<img src="snips-asset:0000…123">`.
They are inlined as `data:` URIs only at the moment of export or clipboard write.

Keeping images out of the HTML body in the database matters: a 2 MB screenshot
base64-encoded inline would make every search query drag the image through memory,
and would bloat the JSON export beyond usefulness.

### 4.3 Schema

```sql
PRAGMA journal_mode = WAL;
PRAGMA foreign_keys = ON;

CREATE TABLE SchemaVersion (
    Version     INTEGER NOT NULL,
    AppliedUtc  TEXT    NOT NULL
);

CREATE TABLE Snippet (
    Id            TEXT    PRIMARY KEY,          -- 19-char zero-padded snowflake
    Name          TEXT    NOT NULL,             -- unique, case-insensitive
    Description   TEXT    NOT NULL DEFAULT '',
    BodyHtml      TEXT    NOT NULL DEFAULT '',  -- canonical; sanitised HTML fragment
    PlainText     TEXT    NOT NULL DEFAULT '',  -- derived, for search + plain paste
    IsRichText    INTEGER NOT NULL DEFAULT 1,
    FolderId      TEXT    NULL REFERENCES Folder(Id) ON DELETE SET NULL,
    IsFavorite    INTEGER NOT NULL DEFAULT 0,
    UseCount      INTEGER NOT NULL DEFAULT 0,
    LastUsedUtc   TEXT    NULL,
    CreatedUtc    TEXT    NOT NULL,
    ModifiedUtc   TEXT    NOT NULL
);
CREATE UNIQUE INDEX IX_Snippet_Name ON Snippet(Name COLLATE NOCASE);
CREATE INDEX IX_Snippet_LastUsed ON Snippet(LastUsedUtc DESC);

CREATE TABLE SnippetAsset (
    Id          TEXT PRIMARY KEY,
    SnippetId   TEXT NOT NULL REFERENCES Snippet(Id) ON DELETE CASCADE,
    ContentHash TEXT NOT NULL,                  -- SHA-256 of Bytes
    MimeType    TEXT NOT NULL,                  -- image/png, image/jpeg
    Bytes       BLOB NOT NULL,
    Width       INTEGER NOT NULL,
    Height      INTEGER NOT NULL
);
CREATE INDEX IX_Asset_Snippet ON SnippetAsset(SnippetId);

CREATE TABLE Shortcut (
    Id          TEXT    PRIMARY KEY,
    SnippetId   TEXT    NOT NULL UNIQUE REFERENCES Snippet(Id) ON DELETE CASCADE,
    Modifiers   INTEGER NOT NULL,               -- MOD_ALT|MOD_CONTROL|MOD_SHIFT|MOD_WIN
    VirtualKey  INTEGER NOT NULL,
    IsEnabled   INTEGER NOT NULL DEFAULT 1
);
CREATE UNIQUE INDEX IX_Shortcut_Combo ON Shortcut(Modifiers, VirtualKey);

CREATE TABLE Variable (
    Id          TEXT PRIMARY KEY,
    Name        TEXT NOT NULL,                  -- unique, case-insensitive, no braces
    Description TEXT NOT NULL DEFAULT '',
    Kind        TEXT NOT NULL,                  -- 'constant' | 'script'
    Language    TEXT NOT NULL DEFAULT 'none',   -- 'none' | 'expr' | 'lua' | 'js'
    Body        TEXT NOT NULL,
    CacheScope  TEXT NOT NULL DEFAULT 'none',   -- 'none' | 'expansion' | 'session'
    CreatedUtc  TEXT NOT NULL,
    ModifiedUtc TEXT NOT NULL
);
CREATE UNIQUE INDEX IX_Variable_Name ON Variable(Name COLLATE NOCASE);

CREATE TABLE Folder (
    Id        TEXT PRIMARY KEY,
    Name      TEXT NOT NULL,
    ParentId  TEXT NULL REFERENCES Folder(Id) ON DELETE CASCADE,
    SortOrder INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE Tag        ( Id TEXT PRIMARY KEY, Name TEXT NOT NULL UNIQUE COLLATE NOCASE );
CREATE TABLE SnippetTag (
    SnippetId TEXT NOT NULL REFERENCES Snippet(Id) ON DELETE CASCADE,
    TagId     TEXT NOT NULL REFERENCES Tag(Id)     ON DELETE CASCADE,
    PRIMARY KEY (SnippetId, TagId)
);

CREATE TABLE Counter ( Name TEXT PRIMARY KEY COLLATE NOCASE, Value INTEGER NOT NULL DEFAULT 0 );
CREATE TABLE Setting ( Key  TEXT PRIMARY KEY, Value TEXT NOT NULL );
```

### 4.4 Storage location

| Mode | Database path | Trigger |
|---|---|---|
| Installed (default) | `%LOCALAPPDATA%\Snips\snips.db` | default |
| Portable | `<folder of Snips.exe>\snips.db` | a file named `portable.txt` sits next to the exe |
| Custom | user-chosen path | set in Settings |

On every schema migration the database file is copied to
`snips.db.backup-<version>-<timestamp>` before any DDL runs.

---

## 5. User interface

### 5.1 Window

- Default size **420 × 560**, resizable, position and size remembered.
- Custom title bar (`WindowStyle=None`, `AllowsTransparency=False`, custom resize
  grips) so the yellow theme extends edge to edge.
- **Pin button** in the title bar toggles `Topmost`. State persisted.
- Closing hides to the tray; the tray menu offers Show / Settings / Quit.
- `Esc` hides the window. The global hotkey shows and focuses it.

### 5.2 Theme

Warm off-yellow, readable, not a highlighter.

| Token | Light | Dark |
|---|---|---|
| Background | `#FFFBEA` | `#2A2620` |
| Surface / list | `#FFF6D8` | `#35302A` |
| Accent | `#E0A800` | `#F0C24B` |
| Text primary | `#2B2B2B` | `#F2EDE3` |
| Text secondary | `#6B6355` | `#B3AA98` |

A **Plain theme** (system colours) is available in Settings for users who find the
yellow distracting or who need high contrast. Windows high-contrast mode forces the
plain theme automatically.

**Icon:** yellow scissors cutting a strip of paper, with a small dashed cut line.
Supplied as SVG plus a multi-resolution `.ico` (16/24/32/48/64/128/256).

### 5.3 Views

| View | Purpose |
|---|---|
| **Picker** (default) | Search + list + preview + output options. The fast path. |
| **Editor** | Create/edit one snippet: name, description, tags, rich body, shortcut. |
| **Variables** | List and edit user-defined variables; test-run them. |
| **Settings** | Hotkeys, storage, startup, paste behaviour, theme, updates. |
| **About** | Version, licence, credits, donation link. |

### 5.4 Picker layout

```
┌───────────────────────────────────────┐
│ ✂ Snips                    📌  ⚙  ─  ✕│  title bar
├───────────────────────────────────────┤
│ 🔍 [ type to filter…                ] │  auto-focused on show
├───────────────────────────────────────┤
│ ▸ Meeting follow-up      Ctrl+Alt+M   │
│ ▸ Invoice header         Ctrl+Alt+I   │  list, virtualised
│ ▸ Signature (formal)                  │
│ ▸ Bug report template                 │
├───────────────────────────────────────┤
│ Preview                          ⌃ ⌄  │  collapsible
│ Dear {{input:Name}},                  │  variables highlighted
│ regarding our meeting of {{date}}…    │
├───────────────────────────────────────┤
│ ☑ Paste into active app               │
│ ☑ Copy to clipboard   ( ) plain ( )rich│
└───────────────────────────────────────┘
```

**Refinement of the brief, please confirm `[?]`:** the brief listed "copy plain text"
and "copy rich text" as two independent checkboxes. A single Windows clipboard holds
*all* formats simultaneously, so those two are not independent — rich content always
carries a plain-text fallback alongside it. Modelled instead as one **Copy to
clipboard** checkbox with a **plain / rich** radio pair. "Plain" strips formatting;
"rich" publishes rich + plain + HTML together.

### 5.5 Keyboard model

| Key | Action |
|---|---|
| Global hotkey (default `Ctrl+Alt+Space`) | Show and focus the picker |
| Type | Filter |
| `↑` `↓` | Move selection |
| `Enter` | Apply, then hide |
| `Ctrl+Enter` | Apply, keep window open |
| `Shift+Enter` | Open in editor |
| `Ctrl+N` | New snippet |
| `Ctrl+E` | Edit selected |
| `Ctrl+D` | Duplicate selected |
| `Ctrl+K` | Define shortcut for selected |
| `Delete` | Delete selected (with confirmation) |
| `Esc` | Hide window |

Right-clicking a row offers the same actions as a context menu, including **"Define a
shortcut…"** as specified in the brief.

### 5.6 Search behaviour

Fuzzy subsequence matching, scored and ranked:

1. Exact name match — highest
2. Name prefix match
3. Name subsequence match (`mfu` matches `Meeting Follow-Up`), scored by how tightly
   the matched characters cluster and whether they land on word boundaries
4. Description / tag match
5. Body match — lowest

The final score is multiplied by a **frecency** factor derived from `UseCount` and
`LastUsedUtc`, so frequently used snippets surface first for ambiguous queries. With
an empty query the list shows most-recently-used first. Matched characters are
highlighted in the list. Target: **under 16 ms** for a 5,000-snippet library, which
comfortably allows filtering in memory on every keystroke.

### 5.7 Editor

- The body editor is a **WebView2** hosting a `contenteditable` surface, with a WPF
  toolbar above it driving it via `document.execCommand` / Selection APIs.
- Rich text editing: bold, italic, underline, strikethrough, bullet and numbered
  lists, indent, text colour, highlight, hyperlink, font size, clear formatting.
- **Edit HTML source** toggle — the canonical form is text, so power users can edit
  it directly. Invalid or disallowed markup is reported rather than silently eaten.
- Images: paste from clipboard, drag-and-drop, or **Insert → Image from file…**
  (PNG/JPEG/GIF/BMP/WebP). Images larger than 2 MB or 2000 px prompt to downscale.
  On save, images are extracted to `SnippetAsset` and the `src` rewritten to
  `snips-asset:<id>`.
- Pasting *into* the editor from a web page runs the same sanitiser as save, so
  scripts and remote references never enter the database.
- **Insert Variable** button opens a searchable palette of all built-in and
  user-defined variables with descriptions and a live sample value.
- Live validation: unknown or malformed `{{…}}` placeholders are underlined with a
  warning squiggle and listed in a footer. Saving with warnings is allowed.
- **Preview** tab renders the snippet with all variables resolved against current
  values, so the user sees the real output before saving.

### 5.8 Shortcut capture

Pressing **Define a shortcut…** opens a small dialog with a capture field that
records the next key combination pressed.

- Requires at least one modifier, or a bare `F1`–`F24`.
- Rejects OS-reserved combinations that Windows intercepts before any application:
  `Ctrl+Alt+Del`, `Win+L`, `Win+D`, `Win+Tab`, `Alt+Tab`, `Ctrl+Shift+Esc`,
  `Win+G`, `Alt+F4`, `PrtScn`.
- Detects duplicates within Snips immediately and refuses to save them.
- Conflicts with *other applications* cannot be detected in advance —
  `RegisterHotKey` simply fails. On failure the shortcut is saved but marked
  **⚠ inactive (in use by another application)** in the list, with a Retry action.
  Snips re-attempts registration for all inactive shortcuts when it regains focus.

---

## 6. Snippet application pipeline

The most delicate part of the system. Executed on the UI thread with an async
continuation, and instrumented so that failures are diagnosable.

```
 1. Capture target        ← already known (see 6.1)
 2. Resolve {{selection}} ← only if the snippet uses it
 3. Resolve variables     ← may show the input form
 4. Build payload         ← plain / RTF / HTML / XamlPackage
 5. Back up clipboard
 6. Set clipboard
 7. Restore target focus
 8. SendInput Ctrl+V
 9. Reposition caret      ← only if {{cursor}} used
10. Restore clipboard     ← after a delay, unless "keep" was requested
```

### 6.1 Knowing where to paste

Snips subscribes to `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` and continuously
tracks the last foreground window **that does not belong to the Snips process**. This
is more reliable than calling `GetForegroundWindow()` at trigger time, because by
then Snips may already have taken focus.

### 6.2 Restoring focus and sending the paste

`SetForegroundWindow` is subject to Windows' foreground lock. The sequence is:

1. `AttachThreadInput(ourThread, targetThread, true)`
2. `SetForegroundWindow(target)` / `BringWindowToTop`
3. `AttachThreadInput(ourThread, targetThread, false)`
4. Wait `PasteDelayMs` (default **60 ms**, configurable 0–500 for slow targets)
5. `SendInput`: Ctrl down, V down, V up, Ctrl up — with any currently held modifiers
   released first, so a lingering `Alt` from the hotkey doesn't turn `Ctrl+V` into
   `Ctrl+Alt+V`.

**UIPI limitation:** a non-elevated process cannot send input to an elevated window.
If the target process cannot be opened for query, Snips detects this, skips the paste,
leaves the content on the clipboard, and shows a non-blocking notice: *"Target app is
running as administrator — content copied to clipboard, press Ctrl+V yourself."*
Running Snips elevated is documented as the workaround but is not the default.

### 6.3 Capturing `{{selection}}`

Only when the snippet actually references `{{selection}}`:

1. Restore focus to the target (§6.2)
2. Record `GetClipboardSequenceNumber()`
3. `SendInput` Ctrl+C
4. Poll for the sequence number to change, up to **300 ms**
5. Read the text; on timeout resolve `{{selection}}` to an empty string

This is the standard technique and it is inherently best-effort — it fails in apps
that do not implement Ctrl+C. That is acceptable and documented.

### 6.4 Clipboard backup and restore

Before setting the payload, Snips snapshots the current clipboard across the formats
it can round-trip (Unicode text, RTF, HTML, DIB, file drop list). It restores that
snapshot **500 ms after** the paste, unless the user chose *Copy to clipboard*, in
which case the snippet is intentionally left there.

This is best-effort by nature: some applications publish delay-rendered or proprietary
clipboard formats that cannot be captured. The behaviour is documented, and the
restore can be switched off in Settings.

### 6.5 `{{cursor}}` handling

`{{cursor}}` marks where the caret should end up after pasting. Windows offers no way
to set another application's caret directly, so Snips counts the characters that
follow the marker and sends that many `Left` key presses after the paste.

Restrictions, stated plainly in the UI: **plain-text output only**, and unreliable in
editors that auto-indent or auto-complete. Ignored for rich-text pastes.

---

## 7. Built-in variables

Syntax: `{{name}}`, `{{name:arg}}`, `{{name:arg1:arg2}}`, with optional filters
`{{name|filter|filter}}`. Names are case-insensitive. A literal `{{` is written `\{{`.

### 7.1 Date and time

All date variables accept an optional **offset** and an optional **.NET format
string**: `{{date:+7d:dd.MM.yyyy}}`. Offset units: `s` `min` `h` `d` `w` `m` `y`.

| Variable | Example output |
|---|---|
| `{{year}}` | `2026` |
| `{{year2}}` | `26` |
| `{{month}}` | `07` |
| `{{day}}` | `21` |
| `{{hour}}` | `11` |
| `{{minute}}` | `46` |
| `{{second}}` | `03` |
| `{{date}}` | `2026-07-21` |
| `{{time}}` | `11:46:03` |
| `{{datetime}}` | `2026-07-21 11:46:03` |
| `{{iso}}` | `2026-07-21T11:46:03+02:00` |
| `{{now:FORMAT}}` | any .NET format string |
| `{{utcnow:FORMAT}}` | same, in UTC |
| `{{timestamp}}` | `1784634363` (Unix seconds) |
| `{{timestampms}}` | Unix milliseconds |
| `{{weekday}}` | `Tuesday` |
| `{{weekday:short}}` | `Tue` |
| `{{monthname}}` | `July` |
| `{{monthname:short}}` | `Jul` |
| `{{week}}` | `30` (ISO-8601 week number) |
| `{{quarter}}` | `Q3` |
| `{{dayofyear}}` | `202` |
| `{{daysinmonth}}` | `31` |
| `{{tomorrow}}` / `{{yesterday}}` | shorthand for `{{date:+1d}}` / `{{date:-1d}}` |
| `{{timezone}}` | `W. Europe Standard Time` |
| `{{utcoffset}}` | `+02:00` |

Culture for month and weekday names follows the OS by default; overridable in
Settings (e.g. force English output on a German Windows).

### 7.2 Identity, system, and paths

| Variable | Meaning |
|---|---|
| `{{user}}` | Windows login name |
| `{{userfullname}}` | Display name from the account |
| `{{useremail}}` | User-configured, from Settings |
| `{{machine}}` | Computer name |
| `{{domain}}` | Domain or workgroup |
| `{{os}}` | `Windows 11 Pro` |
| `{{osversion}}` | `10.0.22000` |
| `{{ip}}` | Primary local IPv4 address |
| `{{home}}` `{{desktop}}` `{{documents}}` `{{downloads}}` `{{temp}}` `{{appdata}}` | Known folder paths |

### 7.3 Context — the high-value ones

| Variable | Meaning |
|---|---|
| `{{clipboard}}` | Current clipboard text |
| `{{selection}}` | Text selected in the target app at trigger time (§6.3) |
| `{{activewindow}}` | Title of the target window |
| `{{activeapp}}` | Process name of the target, e.g. `outlook` |

`{{selection}}` is what enables "wrap the selected text" snippets — quoting it,
translating it into a template, surrounding it with markup. It is worth the
implementation cost.

### 7.4 Snippet metadata

`{{snippetname}}` · `{{snippetid}}` · `{{snippetdescription}}` · `{{usecount}}`

### 7.5 Generators

| Variable | Output |
|---|---|
| `{{guid}}` | `3f2b1a9c-…` |
| `{{guid:n}}` | no hyphens |
| `{{guid:upper}}` | uppercase |
| `{{id}}` | a fresh snowflake ID |
| `{{random}}` | `0`–`99` |
| `{{random:1-100}}` | bounded, inclusive |
| `{{randomstring:12}}` | 12 alphanumeric characters |
| `{{counter:NAME}}` | persistent named counter, post-incremented |
| `{{counter:NAME:+1:0000}}` | with explicit step and format → `0042` |

Counters persist in the `Counter` table and survive restarts — the intended use is
invoice or ticket numbering.

### 7.6 Interactive prompts

If a snippet contains any of these, a small form is shown before the paste. Fields
appear in the order they occur; **repeating the same label reuses one field**, so
`{{input:Name}}` can appear five times and is asked once.

| Variable | Control |
|---|---|
| `{{input:Label}}` | single-line text box |
| `{{input:Label\|default}}` | pre-filled text box |
| `{{multiline:Label}}` | multi-line text box |
| `{{choice:Label\|A,B,C}}` | drop-down |
| `{{datepick:Label:yyyy-MM-dd}}` | date picker |
| `{{check:Label\|yes,no}}` | check box with two output values |

`Esc` in the form cancels the whole operation without pasting.

### 7.7 Filters

Chainable with `|`.

`upper` · `lower` · `title` · `capitalize` · `trim` · `slug` · `urlencode` ·
`urldecode` · `base64` · `unbase64` · `md5` · `sha256` · `reverse` · `left:N` ·
`right:N` · `replace:a,b` · `padleft:N,c` · `padright:N,c` · `escapexml` ·
`escapejson` · `nospaces`

Example: `{{clipboard|trim|title}}` · `{{input:Title|slug}}`

### 7.8 Substitution over rich text

Placeholders may be split across formatting elements — a user might accidentally bold
only half of `{{date}}`, giving `<b>{{da</b>te}}`. Naïve string replacement over the
raw HTML would corrupt the markup. Substitution therefore runs over the **parsed DOM,
never over the HTML string**:

1. Parse `BodyHtml` with AngleSharp into a DOM
2. Concatenate all text nodes in document order into a flat string, retaining an
   offset → (node, index) map
3. Match `{{…}}` patterns against the flat string
4. Resolve each match
5. Write the result into the text node containing the placeholder's **first**
   character, and delete the placeholder's remaining characters from any subsequent
   nodes — working in reverse document order so earlier offsets stay valid
6. Serialise back to HTML

The formatting of the placeholder's first character is therefore what the substituted
value inherits. Resolved values are HTML-escaped unless the variable is explicitly
marked as returning markup.

---

## 8. User-defined variables

### 8.1 Kinds

| Kind | Language | Use |
|---|---|---|
| `constant` | — | A fixed string: company address, standard greeting |
| `script` | `expr` | One-line maths and logic — the default |
| `script` | `lua` | Multi-line procedural logic |
| `script` | `js` | Multi-line procedural logic |

Once defined, a variable named `signature` is used as `{{signature}}`, exactly like a
built-in. User definitions may not shadow built-in names; the editor rejects that at
save time.

### 8.2 Inline expressions

Beyond named variables, a snippet can carry an inline expression:

```
Total incl. VAT: {{= 1250 * 1.19 }}
Due in {{= 30 - dayofmonth }} days
```

Inline expressions always use the `expr` engine.

### 8.3 Engine interface

```csharp
public interface IScriptEngine
{
    string  Language { get; }
    Task<ScriptResult> EvaluateAsync(
        string          source,
        IScriptHost     host,
        CancellationToken ct);
}
```

Adding a language means implementing this interface and registering it. This is the
extension point through which an xTalk-style dialect could later be added (§3.1).

### 8.4 Host API available to scripts

Exposed as `snips.*` in Lua and JavaScript, and as bare identifiers in `expr`:

| Call | Returns |
|---|---|
| `snips.now()` | current local date/time |
| `snips.utcnow()` | current UTC date/time |
| `snips.format(date, fmt)` | formatted date string |
| `snips.clipboard()` | clipboard text |
| `snips.selection()` | selected text in the target app |
| `snips.var(name)` | value of another variable (built-in or user) |
| `snips.setting(key)` | a user setting |
| `snips.counter(name, step)` | increments and returns a persistent counter |
| `snips.input(label, default)` | prompts the user |
| `snips.activeWindow()` | target window title |
| `snips.activeApp()` | target process name |

Lua example:

```lua
local n = tonumber(snips.input("Number of days", "30"))
local due = snips.now() + n * 86400
return "Payment due by " .. snips.format(due, "yyyy-MM-dd")
```

### 8.5 Sandbox

- **No** file system, network, process, OS, or reflection access. Lua's `io`, `os`,
  `package`, and `debug` libraries are not loaded; Jint runs with CLR interop
  disabled.
- **250 ms** wall-clock limit per evaluation, enforced by cancellation token; Jint
  additionally caps statement count and recursion depth.
- Errors never crash the paste. A failing variable resolves to
  `«error: signature — attempt to index a nil value»`, the paste proceeds, and the
  error is surfaced in the picker footer.
- Cyclic references between user variables are detected (depth limit 16) and reported
  as an error rather than hanging.

`CacheScope` controls re-evaluation: `none` (every occurrence), `expansion` (once per
paste — the default, so `{{id}}` in three places gives one value), or `session`.

### 8.6 Variable editor

Two-pane: source on the left, live result on the right, re-evaluated on a 300 ms
debounce. A **Test** button runs the script against the current real context. Syntax
errors are shown inline with line and column.

---

## 9. Output

### 9.1 Options

| Option | Default | Effect |
|---|---|---|
| Show in result field | **always on** | The resolved result is always rendered in the picker's result pane, selectable and copyable by hand |
| Copy to clipboard | **on** | Plain / rich radio. This is the primary mechanism. |
| Paste into active app | off `[?]` | §6 pipeline — sends `Ctrl+V` to the previously focused window |

Defaults are configurable and are remembered per session.

`[?]` **Open question Q7 (§15):** whether auto-paste ships in v1 at all. Clipboard is
now the default and the guaranteed path; auto-paste is a convenience layer on top of
it that can be switched on. It is *not* the same thing as integrating with individual
applications — see Q7.

### 9.2 Clipboard formats published

| Format | Contents | v1 |
|---|---|---|
| `CF_UNICODETEXT` | `PlainText` projection — **always** included as a fallback | Yes |
| `CF_HTML` | The resolved canonical HTML, images inlined as `data:` URIs, wrapped in the CF_HTML header | Yes |
| `CF_RTF` | Converted from HTML if the target matrix shows a need | Phase 6, likely unnecessary |

### 9.3 CF_HTML wrapping

`CF_HTML` is not raw HTML — it requires a byte-offset header that many
implementations get wrong:

```
Version:0.9
StartHTML:00000097
EndHTML:00000null
StartFragment:00000131
EndFragment:00000null
<html><body><!--StartFragment--> … <!--EndFragment--></body></html>
```

The offsets are **byte** counts into the UTF-8 encoding, not character counts — a
snippet containing `Hüttmann`, an em-dash, or an emoji will paste as garbage if this
is computed on `string.Length`. This is written once in `Snips.Interop`, with unit
tests over multi-byte content specifically.

Images are inlined as `data:` URIs at this point, resolved from `SnippetAsset`. This
is the one place where the asset indirection is expanded.

---

## 10. Settings

| Setting | Default |
|---|---|
| Global picker hotkey | `Ctrl+Alt+Space` |
| Start with Windows | off (`HKCU\...\Run`, no admin needed) |
| Start minimised to tray | on |
| Always on top | off |
| Database location | `%LOCALAPPDATA%\Snips\snips.db` |
| Paste delay | 60 ms |
| Restore clipboard after paste | on |
| Restore delay | 500 ms |
| Default output options | Paste on, Copy off |
| Theme | Light yellow |
| Culture for date names | System |
| Check for updates on start | on |
| Donation reminder | on |

### 10.1 Backup, export, import

- **Export** to a single JSON file containing all snippets, variables, folders, tags,
  and shortcuts, with images base64-encoded. Human-readable and diffable, so a user
  can keep their library in their own Git repository.
- **Import** with per-item conflict resolution: skip, overwrite, or keep both.
- **Automatic backup** of the database before every schema migration, and a rolling
  daily backup (last 7 kept) in `%LOCALAPPDATA%\Snips\backups\`.

---

## 11. Licence, copyright, and funding

You asked what these choices actually mean, so in plain terms first.

### 11.1 What a licence choice does

A software licence tells other people what they may legally do with your code.

- **MIT** — "Do anything you like with this, including selling it, as long as you keep
  my copyright notice and don't sue me." Maximally permissive, universally understood,
  no legal friction for anyone. This is what most small open tools use.
- **PolyForm Noncommercial** — "Read it, use it, modify it, but not to make money."
  Prevents someone repackaging Snips and selling it. The trade-off: it is *not*
  recognised as open source, and it blocks legitimate use inside companies, which is
  probably where most of your users would be.
- **GPL** — "Anything you build on this must also be open under the same terms."
  Protects against closed-source forks, but is heavier than a utility like this needs.

### 11.2 Recommendation

**MIT, plus a copyright line, plus a donation ask.**

```
MIT License
Copyright (c) 2026 Roland Hüttmann
```

The `LICENSE` file is written as **UTF-8 without BOM**, which GitHub, Visual Studio,
and every modern toolchain render correctly. Source files carrying the copyright
header use the same encoding, and the repository sets `* text=auto working-tree-encoding=UTF-8`
in `.gitattributes` so no tool downgrades it to a code page.

The realistic risk that someone forks Snips and successfully sells it is very low, and
the cost of a restrictive licence — losing corporate users, losing contributors,
losing the "open source" label — is immediate and certain. MIT is the right trade.

### 11.3 Why not $1 per month

| | $1/month | $15 one-time supporter |
|---|---|---|
| Payment fee | ~$0.33 (33%) | ~$0.74 (5%) |
| You receive | **$0.67** | **$14.26** |
| Infrastructure needed | accounts, licence server, recurring billing, failed-payment handling, refunds | a payment link |
| Ongoing obligation | perpetual — you now owe support | none |

A $1/month subscription on a local utility costs more to operate than it earns, and it
converts the project from a gift into a service you have to run. Comparable tools all
went the other way: **Ditto**, **ShareX**, and **Everything** are free with donation
links and are among the most widely used utilities on Windows.

### 11.4 Recommended funding model

1. **Free and fully functional, forever.** No feature gating, no nag-on-launch, no
   time limit. A crippled tool doesn't get recommended, and recommendations are the
   whole distribution strategy.
2. **GitHub Sponsors** (0% platform fee) and **Ko-fi** (0% on one-off tips) links in
   the README, the About view, and a small permanent ♥ in the title bar.
3. **One reminder, on the honour system.** Shown after **21 days of use** *and* at
   least **50 snippet insertions** — never before, so it only ever reaches people who
   are genuinely getting value.

The reminder is a **dismissible footer bar inside the window**, never a modal, never a
pop-up, never on launch:

> ♥ Snips has saved you about 50 paste operations. If it's useful, consider
> supporting it. — **[Support Snips]** **[Maybe later]** **[I've already donated]**

- *Maybe later* → hidden for 90 days
- *I've already donated* → hidden permanently, **no verification of any kind**

Trusting people costs you nothing here and buys goodwill that a licence check would
destroy.

### 11.5 If you later want real revenue

The path with the least infrastructure is a **Microsoft Store listing with a one-time
"Supporter" in-app purchase** (~$10–15). The Store handles payment, tax, refunds, and
licence verification for a 12% cut, so you never build or operate a billing system.
The supporter unlock should stay cosmetic — a badge, extra themes — so the free
version is never diminished. This is a Phase 4 decision, not a v1 one.

---

## 12. Risks

| # | Risk | Impact | Mitigation |
|---|---|---|---|
| R1 | ~~Custom `FlowDocument` → RTF writer~~ | **Eliminated** | Removed by the HTML decision (§3.3). Canonical storage and clipboard format are now the same string. |
| R1b | CF_HTML byte-offset header computed incorrectly for non-ASCII content | Medium — silent corruption | Single implementation in `Snips.Interop`, unit-tested against umlauts, em-dashes, and emoji (§9.3). |
| R2 | Antivirus flags `SendInput` / hotkey behaviour as keylogger-like | High — blocks adoption | **No low-level keyboard hook in v1** (this is what actually triggers heuristics; it is why auto-expansion is deferred). Store distribution (§16) also carries Microsoft's signature. |
| R3 | SmartScreen warning on the portable exe | Medium — scares off users | **Resolved for the mainstream path** by shipping through the Microsoft Store, where Microsoft re-signs the package (§16). The portable exe on GitHub remains unsigned and documented as such. |
| R8 | WebView2 runtime missing on an old Windows 10 machine | Low | Detect at startup; if absent, the picker and clipboard output still work fully and the editor shows a one-click link to Microsoft's Evergreen bootstrapper. The Store package can declare it as a dependency. |
| R4 | Focus restoration is unreliable in some applications | Medium | `AttachThreadInput` sequence, configurable delay, and a clear fallback message when paste fails. |
| R5 | Clipboard restore is inherently lossy | Low | Best-effort by design, disableable, documented. |
| R6 | Hotkey conflicts with other applications | Medium | Graceful `RegisterHotKey` failure, visible ⚠ marker, retry on focus. |
| R7 | Script sandbox escape | Low — local single-user tool | No I/O surface exposed; timeouts; interpreters run with interop disabled. |

---

## 13. Testing

### 13.1 Automated

- **Template engine** — a large table-driven suite covering every variable in §7,
  every filter, offsets, formats, escaping, malformed input, and nesting.
- **Rich-text substitution** — placeholders split across runs, inside lists, adjacent
  to images.
- **Scripting** — each engine: correct results, timeout enforcement, sandbox denial
  (`io.open` must fail), cyclic reference detection.
- **Snowflake IDs** — monotonic, unique under concurrency, correct zero-padded width.
- **Search** — ranking assertions, and a performance test asserting <16 ms over 5,000
  snippets.
- **Repositories** — CRUD, cascade deletes, migration round-trips against a temp file.

### 13.2 Manual paste-target matrix

Every release is verified against: **Notepad, WordPad, Word, Outlook (compose),
Chrome (textarea + Gmail), Edge, VS Code, Visual Studio, Slack, Teams, OneNote,
Excel (cell), Explorer rename field, and a PowerShell console.**

For each: plain paste, rich paste, image paste, `{{selection}}` capture, `{{cursor}}`
positioning. Results recorded in `docs/compatibility.md` and shipped in the README, so
users know what to expect rather than discovering limits by surprise.

---

## 14. Roadmap

**Phase 1 — Walking skeleton**
Solution structure, SQLite schema and migrations, snowflake IDs, snippet CRUD, plain
picker window, plain-text copy to clipboard. *Goal: storing and retrieving text works.*

**Phase 2 — The fast path**
Global hotkey, fuzzy search, tray icon, result field, clipboard output.
*Goal: the tool is genuinely usable daily.* Auto-paste (foreground tracking, focus
restoration, `SendInput`, clipboard backup/restore) is a self-contained addition here,
subject to Q7.

**Phase 3 — Rich text**
WebView2 `contenteditable` editor, HTML sanitiser, image assets, CF_HTML writer, the
paste-target matrix. *Goal: formatting and images survive the round trip.*

**Phase 4 — Variables**
Built-in variables and filters, rich-text-aware substitution, the interactive input
form, `{{selection}}` and `{{cursor}}`.

**Phase 5 — Scripting**
`expr`, Lua, and JavaScript engines; the variable editor; the sandbox.

**Phase 6 — Polish and release**
Per-snippet shortcuts, settings, export/import, backups, theming, the icon, update
check, donation reminder, README, MSIX packaging, first GitHub release **and Store
submission** (§16).

**Later** — abbreviation auto-expansion (R2 permitting), snippet folders and sync,
optional `CF_RTF` output if the target matrix demands it.

---

## 15. Open questions

| # | Question | My recommendation |
|---|---|---|
| Q1 | IDs: accept 18→19 digit growth, stored zero-padded to 19 chars? (§4.1) | Yes — fixed-width TEXT keeps sorting correct forever |
| Q2 | Replace the two clipboard checkboxes with one checkbox + plain/rich radio? (§5.4) | Yes — a clipboard holds all formats at once, so they were never independent |
| Q4 | Confirm MIT licence and the honour-system donation model (§11) | Recommended as specified |
| Q5 | Should Snips ship with a starter set of example snippets? | Yes — 8–10 examples demonstrating variables teach the syntax far better than documentation |
| Q6 | Folders/tags in v1, or flat list plus search? | Flat + tags. With good fuzzy search, folders are mostly redundant, and they add real UI complexity |
| Q7 | Does **auto-paste** ship in v1? (§9.1) | Keep it, default **off**. It is ~100 lines on top of the clipboard path — not per-app integration — and it is the difference between "a snippet box" and "a tool". Clipboard remains the guaranteed path. |
| Q8 | Are you registering as an **individual** or a **company**? (§16) | Affects Azure Artifact Signing eligibility only; the Store path works either way and is free |

### 15.1 Resolved in this revision

- **Q3 Copyright** → `Roland Hüttmann`, UTF-8 without BOM (§11.2)
- **Body format** → HTML + inline CSS, not RTF or XamlPackage (§3.3)
- **Editor** → WebView2 `contenteditable`, WPF shell retained (§3, §5.7)
- **Primary output** → clipboard, with the result always shown in-window (§9.1)
- **No-warning installs** → Microsoft Store MSIX, free, no certificate (§16)
```

---

## 16. Distribution, signing, and the SmartScreen problem

Requirement: *"I must register such tool with Microsoft, otherwise it is not good for
anybody. It should run without warning dialogs."* Correct, and cheaper than expected.

### 16.1 Where the warnings come from

Two different dialogs get conflated:

1. **SmartScreen** — *"Windows protected your PC · Unknown publisher"*. Triggered by
   an executable with no established reputation. A code-signing certificate plus
   download volume builds that reputation over time.
2. **UAC elevation prompt** — only appears if the installer needs admin rights. Snips
   is per-user and never needs it.

### 16.2 The options, priced

| Route | Cost | SmartScreen | Notes |
|---|---|---|---|
| **Microsoft Store (MSIX)** | **Free** | **None, from day one** | Microsoft re-signs the package. Developer account fees were removed — individual accounts became free in 2025, company accounts in May 2026. Also brings automatic updates and, if ever wanted, in-app purchase. |
| Azure Artifact Signing (formerly Trusted Signing) | $9.99/month | Clears quickly | Microsoft's own signing service; no hardware token. **Eligibility is the catch — see §16.3.** |
| Traditional OV certificate | ~$200–400/year | Warning persists until reputation accrues | Since 2023 the private key must live on a hardware token or cloud HSM. |
| Traditional EV certificate | ~$400–700/year | Immediate | Requires a registered legal business entity; not available to individuals. |
| SignPath.io free OSS tier | Free | Clears over time | Free certificates for qualifying open-source projects. |
| Unsigned portable exe | Free | **Warning every download** | Current draft's plan. |

### 16.3 Eligibility warning on Azure Artifact Signing

Public-trust certificates from Artifact Signing are available to **organisations** in
the USA, Canada, the EU, and the UK — but to **individual developers only in the USA
and Canada**. If you are registering as a private individual in Germany, **this route
is not open to you** unless you sign up as a registered business. It also requires a
paid Azure subscription; free, trial, and sponsored subscriptions are excluded.

This is exactly the kind of detail worth knowing before budgeting for it.

### 16.4 Recommendation — ship both artifacts

| Audience | Artifact | Signing |
|---|---|---|
| **Everyone** | Microsoft Store listing (MSIX) | Microsoft re-signs — **no warning, free, automatic updates** |
| Power users, portable/USB use, corporate machines without Store access | Single-file portable `.exe` on GitHub Releases | Unsigned in v1; documented in the README |

The Store listing satisfies the "no warning dialogs" requirement at **zero cost**,
which is a better outcome than the $200–700/year the traditional certificate route
would have implied. The portable exe stays, because you asked for it and because
Store-restricted corporate environments are real.

**Consequence for the build:** Phase 6 adds an MSIX packaging step
(`Windows Application Packaging Project`, or `dotnet publish` plus `makeappx` — note
`makeappx` is not currently on PATH here and comes with the Windows SDK). MSIX
constraints to design for from the start, none of which conflict with the current
spec:

- No writing next to the executable → **portable mode is disabled in the Store build**;
  the database lives in `%LOCALAPPDATA%\Snips\`.
- Registry `Run` key for start-with-Windows is replaced by the MSIX
  `windows.startupTask` extension.
- Every release goes through Store certification (typically a few days for the first
  submission, faster thereafter).
- `RegisterHotKey`, `SendInput`, and clipboard access are all permitted for packaged
  desktop apps — none of the core functionality is restricted.

### 16.5 Sequencing

Do not submit to the Store until Phase 6. Certification review on an incomplete app
wastes the review cycle and the first impression. Develop and dogfood against the
portable build; package for the Store once the feature set is settled.
