# Language packs — brief and definition

Status: **proposed, not built.** This document defines what a "language pack" means for
Snips and how it would work, so we have something concrete to agree on before any of it
gets built. It condenses a longer discussion from 2026-07-23 into a single reference.

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

## The data shape

```
Language
 ├─ Code (e.g. "de-CH")
 ├─ DisplayName (e.g. "Deutsch (Schweiz)")
 └─ IsRightToLeft (for Arabic)

VariableNameTranslation                — translates a variable's NAME only
 ├─ MasterKey       (the real, English, code-level name — "date", "localdate", ...)
 ├─ LanguageCode    (which language this alias belongs to)
 ├─ LocalName       (what the user types instead — "heute")
 └─ one LocalName can't mean two different MasterKeys in the same language

UiStringTranslation                    — app text, unrelated to variables
 ├─ Key             (e.g. "Settings.Title")
 ├─ LanguageCode
 └─ Value

Variable                               — user-defined constants (already exists, unused)
 ├─ Name            (can itself have translated aliases, same table as above)
 └─ Value

Settings
 └─ "Language" = "de-CH"   — which language is currently active
```

A user's own custom synonym (say, they personally prefer typing `{{heutigesdatum}}`)
uses this exact same mechanism — it's just a `VariableNameTranslation` row scoped to
that person instead of one shipped with the app.

## What ships first, if this gets greenlit

1. **Schema + engine lookup + a language picker in Settings.** Variable name translation
   and locale-aware formatting (`{{localdate}}` already exists and is a step in this
   direction) work; app dialogs stay English.
2. **App text translation.** Menus, dialogs, messages.
3. **Right-to-left layout** for Arabic — a real layout change, not just text.
4. **User-editable synonym dictionary**, reusing the Phase 1 mechanism.

Target languages: English, German, French, Italian, Spanish, Russian, Chinese, Arabic —
with user-defined additions/variations always possible on top, per the mechanism above.

This is a multi-week scope once started, not a quick add-on — flagging that plainly so
it gets scheduled deliberately rather than picked up piecemeal.
