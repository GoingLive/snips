# Language packs — brief and definition

Status: **Phase 1 shipped 2026-07-24.** Schema, engine lookup, a language picker in
Settings, and a translation editor ("Settings → Manage translations…") are built and
working. What's NOT done yet: any actual translation *content* (the tables ship empty —
deliberately not guessing German/French/etc. wording; that's Roland's call to make or
delegate to a translator, the editor is the tool for it) and Phases 2-4 below (app text
translation, RTL layout, user-editable synonyms).

## What a language pack is

A language pack is a set of translations for one language (German, French, Chinese,
...) covering two independent things:

1. **The app's own text** — menu items, dialog titles, button labels, messages like
   "Delete 'X'?". This is standard app localization; nothing Snips-specific about it.
2. **Variable names** — so a German user can type `{{heute}}` instead of `{{date}}` and
   get the same result. This part is specific to how Snips works and is the more
   interesting design question.

## The core rule: one master, many aliases

Every variable has exactly one **master name** — the English identifier already built
into Snips (`date`, `localdate`, `snippetname`, ...). A language pack never creates a
second, independent version of a variable's behavior; it only adds translated **names**
that point back to the same master. `{{heute}}` and `{{date}}` would always do exactly
the same thing — offsets, custom formats, filters, everything — because under the hood
they resolve to the identical code path.

**Why not let each language have its own independent variable?** Because then a bug fix,
a new offset unit, or a new filter would need to be re-implemented and re-tested in
every language separately, and they could quietly drift apart. One master keeps Snips's
actual behavior singular; translation only changes what you're allowed to *call* it.

## Variants (like "today" vs. "today, ISO format")

Rather than inventing per-language variant names (`heute`, `heute.iso`, `heute_kurz`, ...),
variants stay in Snips's existing filter/format grammar, which already solves this:

```
{{heute}}              the German name for {{date}}
{{heute|iso}}          same variable, run through a new "iso" filter
{{heute:dd.MM.yyyy}}   same variable, with a custom format argument
```

One mechanism for "I want this variable to behave differently," reused everywhere,
instead of a second naming scheme layered on top.

## What decides which language is active

The user picks a language once, in Settings. That choice determines which translated
variable names and which UI strings are active. It has to be set before someone starts
typing snippets with translated variable names in them — otherwise `{{heute}}` would be
meaningless until the setting is applied.

## The data shape (as built)

```
Language                               — src/Snips.Data/Migrations (schema v2)
 ├─ Code (e.g. "de" — the fixed SupportedLanguages list, not full locale tags like "de-CH")
 ├─ DisplayName (e.g. "Deutsch")
 └─ IsRightToLeft (for Arabic)

VariableNameTranslation                — translates a variable's NAME only
 ├─ MasterKey       (the real, English, code-level name — "date", "localdate", ...;
 │                    the authoritative list is BuiltInVariableCatalog in Snips.Core)
 ├─ LanguageCode    (which language this alias belongs to)
 ├─ LocalName       (what the user types instead — "heute")
 └─ one LocalName can't mean two different MasterKeys in the same language (unique index);
    application logic (not the schema) keeps one MasterKey to one LocalName per language,
    leaving room to relax that later if real demand for synonyms shows up

UiStringTranslation                    — NOT YET BUILT (Phase 2). App text, unrelated to
                                          variables: menu items, dialog titles, messages.

Variable                               — user-defined constants (schema exists, unused —
 ├─ Name              still no UI to create one; a natural Phase 4 extension point)
 └─ Value

Settings
 └─ "Language" = "de"   — which language is currently active (MainWindow reads this and
                           loads VariableNameTranslation rows for it into every render)
```

A user's own custom synonym (say, they personally prefer typing `{{heutigesdatum}}`)
uses this exact same mechanism — it's just a `VariableNameTranslation` row, editable the
same way as any shipped translation.

## The editor

Settings → "Manage translations…" opens a grid: one row per `BuiltInVariableCatalog`
entry (English name + a short meaning), a text box for the translated name, and a
language dropdown. Untranslated rows are highlighted so coverage is visible at a glance —
a caption at the bottom also states it as "N of M translated." Reads and writes the
`VariableNameTranslation` table directly; there's no separate file format to keep in
sync with the database.

## What ships first, if this gets greenlit

1. **Schema + engine lookup + a language picker in Settings + the editor above.** DONE.
   Variable name translation and locale-aware formatting (`{{localdate}}`) work; app
   dialogs stay English. Translation *content* is still empty — nobody's filled in
   German/French/etc. words yet, on purpose.
2. **App text translation.** Menus, dialogs, messages. Not started.
3. **Right-to-left layout** for Arabic — a real layout change, not just text. Not started.
4. **User-editable synonym dictionary**, reusing the Phase 1 mechanism (already works
   mechanically today — anyone with access to the editor can add a personal synonym; what's
   missing is a per-user-scoped version of it as opposed to the single shared table today).

Target languages: English, German, French, Italian, Spanish, Russian, Chinese, Arabic —
with user-defined additions/variations always possible on top, per the mechanism above.
