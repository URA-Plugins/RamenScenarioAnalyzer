# RamenScenarioAnalyzer

`RamenScenarioAnalyzer` renders Ramen scenario training information in a workspace when a response contains `chara_info`, `ramen_data_set`, `home_info.command_info_array`, and the five base training commands. Each rendered analyzer response switches to the Ramen workspace.

The analyzer has no runtime dependency on `EventLoggerPlugin`. Other plugins can reference this project, declare `RamenScenarioAnalyzer` in manifest `Dependencies`, verify it with `IPluginContext.IsPluginAvailable("RamenScenarioAnalyzer")`, and then use `RamenTrainingDisplay.RegisterModifier` for optional panel integration.

Successful training displays are retained in process memory by `(single_mode_chara_id, turn)`. Reprocessing the same key replaces that record in place. Use ↑/↓ for the previous/next record and ←/→ for the oldest/newest record while the Ramen panel has focus; use PageUp/PageDown, Home/End, or the mouse wheel to scroll its content.

The per-plugin setting is stored only after Save in `PluginData/RamenScenarioAnalyzer/settings.json`:

```json
{
  "historyLimit": 100
}
```

`historyLimit` accepts `0` through `1000` and defaults to `100` when the file is absent. `0` disables history while retaining the latest live display. History entries are not persisted across plugin reloads.

The default display follows the same general layout as `BreedersScenarioAnalyzer`: date/status panels, important information, scenario panels, then five horizontal training cards with Extras in a separate right column. Command scenario reward totals are shown beside each training level when present; training counts, active effects, and the last command result are shown in the scenario or extra areas.

`RegisterModifier` returns an `IDisposable` registration applied in order after every default panel build. `RefreshCurrent(false)` rebuilds the current display with all registrations, while `ModifyCurrent` adds a one-time final modification. Registration, removal, and refresh replace the current history key in place without unread state or workspace switching. The public Important, Extra, training-card, and scenario-panel editors accept plain text or colored `RamenDisplaySegment` values. A failing modifier leaves the last successful display unchanged.
