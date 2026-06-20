# SyncBarCleaner

SyncBarCleaner is a small experimental Dalamud plugin for Final Fantasy XIV.

It visually hides actions on controller cross hotbars when those actions are above the player’s current level-synced level. It is intended for players who use fixed high-level hotbar layouts but want synced duties to look cleaner and easier to read.

## What it does

SyncBarCleaner hides the icon and small value text for actions that are not available at the current synced level.

It currently supports:

* Main cross hotbar: XHB1
* Expanded/WXHB left side: XHB2 slots 0–7
* Expanded/WXHB right side: XHB2 slots 8–15

## What it does not do

SyncBarCleaner does not edit hotbar data.

It does not:

* Remove actions from hotbars
* Move actions
* Save or change hotbar layouts
* Touch macros, items, gearsets, duty actions, mounts, or other non-action slots

It only changes the visibility of specific rendered UI nodes.

## Commands

```text
/syncbar status
```

Shows current plugin status, sync state, player level, and effective level.

```text
/syncbar auto
```

Enables real level-sync mode. This hides unavailable actions only when the game reports that the player is level synced.

```text
/syncbar auto <level>
```

Enables test mode using a fake effective level.

Example:

```text
/syncbar auto 50
```

This is useful for checking what the hotbar would look like at level 50.

```text
/syncbar auto off
```

Disables auto-hide and restores hidden icons/text.

```text
/syncbar restore
```

Forces a visual restore of supported cross hotbars.

## Current status

Version 0.1 is a personal-use proof of concept.

Tested successfully on healer, tank, and DPS jobs across level-synced content from low levels up to 80+.

## Safety notes

This plugin is experimental and uses native UI node manipulation. It has been kept deliberately narrow: it only touches the visual icon/text leaves that were tested as safe.

Use at your own risk.

## License

This project is licensed under AGPL-3.0.
