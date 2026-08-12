# RamenScenarioAnalyzer

`RamenScenarioAnalyzer` renders Ramen scenario training information in a workspace when a response contains `chara_info`, `ramen_data_set`, `home_info.command_info_array`, and the five base training commands. Each rendered analyzer response switches to the Ramen workspace.

The built-in display also reads `EventLoggerPlugin` summary data and appends available event statistics to the training panel. `EventLoggerPlugin` must be installed with this plugin.

Successful training displays are retained in process memory by `(single_mode_chara_id, turn)`. Reprocessing the same key replaces that record in place. Use ↑/↓ for the previous/next record and ←/→ for the oldest/newest record while the Ramen panel has focus; use PageUp/PageDown, Home/End, or the mouse wheel to scroll its content.

The per-plugin setting is stored only after Save in `PluginData/RamenScenarioAnalyzer/settings.json`:

```json
{
  "historyLimit": 100
}
```

`historyLimit` accepts `0` through `1000` and defaults to `100` when the file is absent. `0` disables history while retaining the latest live display. History entries are not persisted across plugin reloads.

The default display follows the same general layout as `BreedersScenarioAnalyzer`: date/status panels, important information, scenario panels, then five horizontal training cards with Extras in a separate right column. Command scenario reward totals are shown beside each training level when present; training counts, active effects, and the last command result are shown in the scenario or extra areas.
