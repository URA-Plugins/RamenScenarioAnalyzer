# RamenScenarioAnalyzer

`RamenScenarioAnalyzer` renders Ramen scenario training information in a workspace when a response contains `chara_info`, `ramen_data_set`, `home_info.command_info_array`, and the five base training commands. Each rendered analyzer response switches to the Ramen workspace.

The built-in display also reads `EventLoggerPlugin` summary data and appends available event statistics to the training panel. `EventLoggerPlugin` must be installed with this plugin.

The default display follows the same general layout as `BreedersScenarioAnalyzer`: date/status panels, important information, scenario panels, then five horizontal training cards with Extras in a separate right column. Command scenario reward totals are shown beside each training level when present; training counts, active effects, and the last command result are shown in the scenario or extra areas.
