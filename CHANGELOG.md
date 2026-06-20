\# Changelog

## v0.1.2 - Settings and testing controls

### Added

* Added a real SyncBarCleaner settings window.
* Added setting to enable SyncBarCleaner automatically on plugin load.
* Added setting to enable or disable main cross hotbar support.
* Added setting to enable or disable expanded/WXHB cross hotbar support.
* Added persistent fake level test mode with configurable effective level.
* Added a settings-window restore button for hidden icons.

### Changed

* Registered the plugin main UI callback so Dalamud no longer reports a missing main UI validation warning.
* Settings window is now resizable and scrollable.
* Main cross hotbar hiding now respects the visible SET label instead of assuming SET 1.

### Fixed

* Fixed incorrect hiding when `_ActionCross` displays non-main sets such as SET 6.
* Fixed sample plugin configuration options still appearing in the settings window.
* Removed temporary debug text-dump code from the stable path.


\## v0.1.0



\- Added automatic hiding of unavailable level-synced actions on XHB1.

\- Added support for expanded/WXHB XHB2 left and right bars.

\- Added `/syncbar auto`, `/syncbar auto <level>`, `/syncbar auto off`, `/syncbar restore`, and `/syncbar status`.

\- Added auto-enable on plugin load.

\- Removed debug node-scanning commands from the stable build.

\- Confirmed testing across healer, tank, and DPS jobs from low-level synced content up to 80+.

