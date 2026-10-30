# RamenScenarioAnalyzer

`RamenScenarioAnalyzer` renders Ramen scenario training information in a workspace when a response contains `chara_info`, `ramen_data_set`, `home_info.command_info_array`, and the five base training commands. Each rendered analyzer response switches to the Ramen workspace.

The analyzer has no runtime dependency on `EventLoggerPlugin`. Other plugins can reference this project, declare `RamenScenarioAnalyzer` in manifest `Dependencies`, verify it with `IPluginContext.IsPluginAvailable("RamenScenarioAnalyzer")`, and register a part producer through `RamenTrainingDisplay.RegisterPartProducer("MyPlugin")`.

`RamenScenarioState.Snapshot()` exposes the current training's cached state. Load populates it even when pending events prevent the training panel from rendering. `loaded` means a Load was received for `single_mode_chara_id`; partial updates for another training clear previous fields and stay unloaded until Load. Plugin disposal clears the cache.

Successful training displays are retained in process memory by `(single_mode_chara_id, turn)`. Reprocessing the same key replaces that record in place. Use ↑/↓ for the previous/next record and ←/→ for the oldest/newest record while the Ramen panel has focus; use PageUp/PageDown, Home/End, or the mouse wheel to scroll its content.

The per-plugin setting is stored only after Save in `PluginData/RamenScenarioAnalyzer/settings.json`:

```json
{
  "historyLimit": 100
}
```

`historyLimit` accepts `0` through `1000` and defaults to `100` when the file is absent. `0` disables history while retaining the latest live display. History entries are not persisted across Host restarts.

The default display follows the same general layout as `BreedersScenarioAnalyzer`: date/status panels, important information, scenario panels, then five horizontal training cards with Extras in a separate right column. Command scenario reward totals are shown beside each training level when present; training counts, active effects, and the last command result are shown in the scenario or extra areas.

`RegisterPartProducer(sourceTitle)` requires a non-empty single-line title. `producer.Update(new RamenTrainingDisplayId(charaId, turn), part)` replaces only that producer's part for the specified display ID and does not touch the View. `RamenTrainingDisplay.Show(id)` combines that ID's scenario base part and producer parts, then publishes once; it returns `false` when the scenario part is not available or cancellation was requested. A non-empty analyzer Extra starts with the cyan `Ramen` title, followed by each non-empty producer Extra under its cyan title in registration order; sections are contiguous with no inner border, indentation, or blank separator. Same-producer updates are last-write-wins, different IDs are isolated, and registration, Update, and Dispose never publish implicitly. The public Important, Extra, training-card, and scenario-panel editors accept plain text or colored `RamenDisplaySegment` values. A failing part leaves the last successful display unchanged.

## Build

The repository references the Host API through NuGet. From the repository root after cloning:

```powershell
dotnet build .\RamenScenarioAnalyzer.csproj -c Release -m:1 -p:GenerateUraPluginManifestOnBuild=false -p:PackageUraPluginOnBuild=false -p:DeployUraPluginToLocalAppDataOnBuild=false
```

The Host-dependent smoke executable is maintained at `tests/RamenScenarioAnalyzerSmoke` and runs 21 phases covering display composition, Terminal.Gui layout and colors, scrolling and resizing, training-partner semantics, analyzer registration and dispatch, workspace and state-cache lifecycle, producer composition, and keyed history input.

## 验证与发布

在 Windows 仓库根执行 `act workflow_dispatch --artifact-server-path "$env:TEMP/ura-act-artifacts"`。本地与 GitHub 使用同一份 workflow；版本 tag 触发 GitHub Release 发布。环境要求、共用 workflow 本地映射和发布规则见 [URA plugin workflows](https://github.com/URA-Plugins/.github/blob/v1/README.md)。

## 本仓测试

`tests/RamenScenarioAnalyzerSmoke` 默认运行本插件的生命周期、历史配置和显示检查，并检查发布边界。测试工具由本仓 `tests/Support` 持有。

```powershell
$env:URA_TEST_PLUGIN_ROOT = (Get-Location).Path
dotnet run --project tests/RamenScenarioAnalyzerSmoke -p:UraTestHostRoot=<Host源码绝对路径> -p:GenerateUraPluginManifestOnBuild=false -p:PackageUraPluginOnBuild=false -p:DeployUraPluginToLocalAppDataOnBuild=false
```

默认测试入口还执行本插件 fresh ZIP 的加载、业务与清理。先将本轮 Release/win-x64 ZIP 放入目录，并设置绝对路径 `URA_PLUGIN_SMOKE_PACKAGE_ROOT`；GitHub Actions 使用该变量或本工作区 `artifacts`。不会读取安装目录中的插件包。

`tests/RamenScenarioAnalyzerReplay` 默认运行 HTTP 解析与三语言错误识别自测；`-- --corpus <绝对语料目录>` 回放该目录，仅执行 Ramen 插件并报告分发、配对、可见错误和清理结果。错误判定限于当前 framebuffer 中可见的 Error，不扫描其他工作区或屏幕外日志。没有语料时不声明真实回放通过。
