# RamenScenarioAnalyzer

`RamenScenarioAnalyzer` renders Ramen scenario training information in a workspace when a response contains `chara_info`, `ramen_data_set`, `home_info.command_info_array`, and the five base training commands. Each rendered analyzer response switches to the Ramen workspace.

The analyzer has no runtime dependency on `EventLoggerPlugin`. Other plugins can reference this project, declare `RamenScenarioAnalyzer` in manifest `Dependencies`, verify it with `IPluginContext.IsPluginAvailable("RamenScenarioAnalyzer")`, and register a part producer through `RamenTrainingDisplay.RegisterPartProducer("MyPlugin")`.

Successful training displays are retained in process memory by `(single_mode_chara_id, turn)`. Reprocessing the same key replaces that record in place. Use ↑/↓ for the previous/next record and ←/→ for the oldest/newest record while the Ramen panel has focus; use PageUp/PageDown, Home/End, or the mouse wheel to scroll its content.

The per-plugin setting is stored only after Save in `PluginData/RamenScenarioAnalyzer/settings.json`:

```json
{
  "historyLimit": 100
}
```

`historyLimit` accepts `0` through `1000` and defaults to `100` when the file is absent. `0` disables history while retaining the latest live display. History entries are not persisted across plugin reloads.

The default display follows the same general layout as `BreedersScenarioAnalyzer`: date/status panels, important information, scenario panels, then five horizontal training cards with Extras in a separate right column. Command scenario reward totals are shown beside each training level when present; training counts, active effects, and the last command result are shown in the scenario or extra areas.

`RegisterPartProducer(sourceTitle)` requires a non-empty single-line title. `producer.Update(new RamenTrainingDisplayId(charaId, turn), part)` replaces only that producer's part for the specified display ID and does not touch the View. `RamenTrainingDisplay.Show(id)` combines that ID's scenario base part and producer parts, then publishes once; it returns `false` when the scenario part is not available or cancellation was requested. A non-empty analyzer Extra starts with the cyan `Ramen` title, followed by each non-empty producer Extra under its cyan title in registration order; sections are contiguous with no inner border, indentation, or blank separator. Same-producer updates are last-write-wins, different IDs are isolated, and registration, Update, and Dispose never publish implicitly. The public Important, Extra, training-card, and scenario-panel editors accept plain text or colored `RamenDisplaySegment` values. A failing part leaves the last successful display unchanged.

## Build

The repository pins the Host source with a Git submodule. From the repository root after cloning:

```powershell
git submodule update --init --recursive
dotnet build .\RamenScenarioAnalyzer.csproj -c Release -m:1 -p:RuntimeIdentifier=win-x64 -p:SelfContained=false -p:PlatformTarget=AnyCPU -p:DeployUraPluginToLocalAppDataOnBuild=false
dotnet run --project .\tests\RamenScenarioAnalyzerPatchDslSmoke\RamenScenarioAnalyzerPatchDslSmoke.csproj -c Release -m:1 -p:GenerateUraPluginManifestOnBuild=false -p:PackageUraPluginOnBuild=false -p:DeployUraPluginToLocalAppDataOnBuild=false
```
