# Snips variables — a plain-English guide

This is the friendly version. If you want the complete technical reference (every
variable, every option), that's [`variables.yaml`](variables.yaml) in this same folder —
this guide is for getting things done without reading code.

## What is a variable, again?

When you're writing a snippet, anything wrapped in double curly braces — like `{{date}}` —
gets replaced with a real value the moment you use the snippet. You type this once:

```
Dear {{input:Name}},

Thank you for your message on {{date}}. I'll get back to you by {{date:+3d:dd.MM.yyyy}}.

Best regards,
{{user}}
```

...and every time you use it, Snips fills in today's date, asks you for a name, and
signs it with your Windows name automatically. That's the whole idea.

**Tip:** you don't have to memorize any of this. Open the snippet editor and look at the
"Variables" panel on the right — it's searchable, and double-clicking an entry types it
in for you at the cursor.

## The basics you'll use constantly

| You type | You get |
|---|---|
| `{{date}}` | Today's date, e.g. `2026-07-23` |
| `{{time}}` | Right now, e.g. `18:33:29` |
| `{{datetime}}` | Both together |
| `{{user}}` | Your Windows name |
| `{{useremail}}` | Your email (set once in the tray icon's Settings) |
| `{{clipboard}}` | Whatever you last copied |

## Dates that look the way YOU want

By default `{{date}}` prints `2026-07-23`. If you want it to look different — say,
`23.07.2026` — add a format after a colon:

```
{{date:dd.MM.yyyy}}   ->  23.07.2026
{{date:MM/dd/yyyy}}   ->  07/23/2026
```

Build your own format from these building blocks:

| Piece | Means | Example |
|---|---|---|
| `dd` | day, 2 digits | 23 |
| `MM` | month, 2 digits (capital M!) | 07 |
| `MMMM` | month, spelled out | July |
| `yyyy` | year, 4 digits | 2026 |
| `HH` | hour, 24-hour clock | 18 |
| `mm` | minutes (lowercase m!) | 33 |
| `ss` | seconds | 29 |

**The one thing that trips everyone up:** capital `M` is month, lowercase `m` is
minutes. `{{date:mm.MM.yyyy}}` would swap them by accident. If a format ever comes out
wrong, this is the first thing to check.

You can mix any of these pieces together, in any order, with any punctuation between
them (dots, dashes, slashes, spaces, colons — all fine):

```
{{date:dd.MM.yyyy}}          ->  23.07.2026
{{datetime:yyyy-MM-dd HH:mm}}  ->  2026-07-23 18:33
{{time:HH-mm}}                ->  18-33
```

If you don't want to build your own format, these are ready-made:

| You type | You get |
|---|---|
| `{{localdate}}` | Short date the way your Windows language shows it |
| `{{localtime}}` | Short time the same way |
| `{{locallongdate}}` | The long version, e.g. "Thursday, 23 July 2026" |
| `{{intldate}}` | Always spelled-out English, e.g. "23 July 2026" — good for messages that need to read the same no matter who opens them |
| `{{tomorrow}}` / `{{yesterday}}` | Exactly what they say |
| `{{weekday}}` | Day name, e.g. "Thursday" |

Want a date a few days in the future or past? Add an offset before the format:

```
{{date:+7d}}              ->  a week from today, default format
{{date:+7d:dd.MM.yyyy}}   ->  a week from today, your format
{{date:-3d}}               ->  3 days ago
```
Offsets: `d` = days, `w` = weeks, `m` = months, `y` = years, `h` = hours, `min` = minutes.

## Asking you a question when you use the snippet

Some variables pop up a little form instead of filling in something automatic:

| You type | What happens |
|---|---|
| `{{input:Name}}` | Asks for a value, calls the field "Name" |
| `{{input:Name:Anonymous}}` | Same, but the box starts pre-filled with "Anonymous" (edit or clear it as needed) |
| `{{choice:Size:S,M,L}}` | Asks you to pick one of S / M / L |
| `{{datepick:When}}` | Asks you to pick a date |
| `{{check:Confirmed}}` | Asks for a yes/no checkbox |

If you use `{{input:Name}}` twice in the same snippet, you're only asked once — both
spots get the same answer.

## Making the text fancier

Add `|` after any variable to transform it:

```
{{clipboard|upper}}   ->  WHATEVER WAS ON YOUR CLIPBOARD, SHOUTED
{{user|lower}}         ->  your windows name, lowercased
```

## Where things come from on your computer

| You type | You get |
|---|---|
| `{{machine}}` | Your computer's name |
| `{{os}}` | "Windows 11 Pro" |
| `{{home}}` | Your user folder |
| `{{activewindow}}` / `{{activeapp}}` | The window/app Snips is about to paste into |

## Random and one-off values

| You type | You get |
|---|---|
| `{{guid}}` | A random unique ID |
| `{{random:1-100}}` | A random number in that range |
| `{{counter:Invoice}}` | A number that goes up by 1 every time this snippet is used — great for invoice numbers |

## Company info without typing it every time (advanced)

If there's a piece of text you want available everywhere — a company name, a support
address — without it living inside Snips itself, you can create a small file named
`external-variables.json` next to Snips' database and put simple entries in it:

```json
{
  "companyname": "Acme AG",
  "supportemail": "support@acme.example"
}
```

Then `{{companyname}}` and `{{supportemail}}` work like any other variable. Snips
re-reads this file every time, so another program could update it and Snips would pick
up the change immediately. This one's a bit more technical — ask if you'd like a hand
setting it up.

## Still stuck?

Anything you type that isn't a real variable name is left exactly as you typed it
(`{{roland}}` stays `{{roland}}`) rather than silently disappearing — so if something in
your snippet still shows curly braces after using it, that's Snips telling you it didn't
recognize that name. Check the searchable list in the snippet editor, or the full
technical reference in [`variables.yaml`](variables.yaml).
