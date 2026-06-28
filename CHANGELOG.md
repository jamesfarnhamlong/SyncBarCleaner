## v1.0.0 - Stable private release

### Added
- Stable automatic hiding for unavailable level-synced actions on controller cross hotbars.
- Support for the main cross hotbar and expanded/WXHB cross hotbars.
- Dynamic visible SET detection for `_ActionCross`, preventing incorrect hiding on alternate sets such as utility bars.
- Settings window with toggles for automatic enable, main cross hotbar support, expanded cross hotbar support, and fake level testing.
- Manual restore command and settings-window restore button.

### Fixed
- Prevented items, macros, mounts, gearsets, Limit Break, and other non-action slots from being hidden.
- Fixed incorrect visual hiding when `_ActionCross` displayed non-main cross hotbar sets.
- Fixed Dalamud validation warning by registering a main UI callback.
- Removed SamplePlugin default settings UI.

### Notes
- This plugin only changes visibility/alpha on specific UI leaf nodes.
- It does not edit hotbar data, clear slots, move actions, or save layout changes.

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

