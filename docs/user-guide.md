# Snips — user guide

This covers using the app itself. For the variables you can put inside a snippet
(`{{date}}`, `{{user}}`, and so on), see the separate
[variables guide](variables-guide.md).

## What Snips is

Snips stores short pieces of text you use often — email signatures, canned replies,
templates — and lets you drop one into whatever you're working on with a couple of
keystrokes, instead of retyping or hunting through old emails to copy-paste from.

## Where it lives

Snips runs quietly in the background and doesn't show a window until you ask for one.
Look for its icon in the system tray (the small icons near the clock, bottom-right of
your screen) — Windows sometimes hides it under the little ^ arrow if you haven't
pinned it visible.

You can open the picker window three ways, and they all do the same thing:
- Press the hotkey (shown in the tray icon's tooltip — hover over it to see which one)
- Click the tray icon
- Double-click the desktop shortcut

If Snips is already running and you double-click the shortcut again, it won't start a
second copy — it just brings the existing window to the front. Only one Snips runs at a
time.

## The picker window

This is the main window: a search box at the top, your list of snippets below it, a
preview of what will actually be pasted, and two checkboxes at the bottom.

- **Type to search** — narrows the list as you type, matching against the snippet's
  name, description, and body text.
- **Click a snippet** (or use the arrow keys) — the preview box updates immediately,
  showing the *resolved* text (today's date filled in, your name filled in, etc., not
  the raw `{{...}}`). Clicking a snippet that's already selected refreshes the preview
  again, which matters for anything time-based like `{{now}}`.
- **Press Enter** — applies the selected snippet according to the two checkboxes below.
- **Ctrl+Enter** — applies the snippet but keeps the window open, for firing off several
  snippets in a row without reopening Snips each time.
- **Double-click a snippet** — opens it for editing (see below), it does not apply it.
- **Escape**, or the window's close button — hides the window without applying
  anything. Snips keeps running in the tray either way; this never quits it.

## Copy vs. Paste

| Checkbox | What it does |
|---|---|
| **Copy to clipboard** | Puts the resolved text on your clipboard. On by default. |
| **Paste into active app** | Also tries to type it directly into whatever window you had open before you switched to Snips. Off by default. |

Both checkboxes remember your last choice — check "Paste into active app" once and it
stays checked next time you open Snips, rather than resetting.

**How to actually use "Paste into active app":**
1. Click into the app you want the text to land in (Notepad, an email, a chat window —
   whatever it is), so your cursor is sitting where you want the text.
2. *Without closing that app*, open Snips (hotkey, tray icon — doesn't matter which).
3. Make sure "Paste into active app" is checked.
4. Select a snippet and press Enter.

Snips hides itself, switches back to the app from step 1, and types the text in as if
you'd pressed Ctrl+V. The key part is step 1 — Snips remembers whichever window was
active *right before* it opened, and that's the only window it will try to paste into.
If you open Snips first and only afterward click into the app you meant to paste into,
there's no target captured and it falls back to just copying to the clipboard (which
you can then paste yourself with Ctrl+V).

If a paste attempt doesn't work, the status line at the bottom of the picker will say
why (e.g. "Windows refused the switch" or "target window is gone") rather than staying
silent — that message is worth reading if something seems off, and worth reporting back
if it doesn't match what you expected.

## Creating, editing, and deleting snippets

- **New** — the button next to the search box, or Ctrl+N.
- **Edit** — double-click a snippet in the list, or Ctrl+E, or right-click → Edit.
- **Duplicate** — right-click → Duplicate, or Ctrl+D. Makes a copy named "X (copy)" that
  you can then modify without touching the original.
- **Delete** — right-click a snippet → Delete, or press the Delete key with a snippet
  selected, or open it for editing and use the Delete button there. Always asks for
  confirmation first.

Inside the editor, the panel on the right lists every variable Snips understands, with a
search box above it — type to filter, double-click an entry to insert it at your cursor.

## Giving a snippet its own shortcut

Right-click a snippet → "Define a shortcut…" (or Ctrl+K) to give it a personal global
hotkey that applies it directly, without opening the picker at all. Useful for the one
or two snippets you use constantly.

## Settings

Reachable from the tray icon's right-click menu → Settings. Currently just your email
address, used by the `{{useremail}}` variable — nothing else reads from here yet.

## The tray icon menu

Right-click the tray icon for: **Show Snips**, then your 9 most recently used snippets
(click one to apply it immediately — copied and pasted, without opening the picker at
all), then **Settings…** and **Quit**.

Quitting from here is the only way to actually stop Snips — closing the picker window
just hides it, the same as pressing Escape.
