# RamenScenarioAnalyzer

`RamenScenarioAnalyzer` renders Ramen scenario training information through LiveDisplay when a response contains `chara_info`, `ramen_data_set`, `home_info.command_info_array`, and the five base training commands.

The built-in display also reads `EventLoggerPlugin` summary data and appends available event statistics to the training panel. `EventLoggerPlugin` must be installed with this plugin.

The default training cards follow the same general layout as `BreedersScenarioAnalyzer`: date/status panels, important information, scenario panels, then the five training cards. Command scenario reward totals are shown beside each training level when present; training counts, active effects, and the last command result are shown in the scenario or extra areas.

Plugins that need to change the training display can reference this project and share the same load context:

```csharp
using RamenScenarioAnalyzer;
using Spectre.Console;
using UmamusumeResponseAnalyzer.Plugin;

[assembly: SharedContextWith("RamenScenarioAnalyzer")]

RamenTrainingDisplay.Modify((context, display) =>
{
    display.Training.Modify(RamenTrain.Speed, card =>
    {
        card.AddDescription("友情人数多时优先考虑");
        card.SetBorder(Color.LightGreen);
    });

    display.Training.ModifyByCommandId(105, card =>
    {
        card.AddMarkup("[yellow]耐力训练补充说明[/]");
    });

    display.Scenario.Modify("uraf", panel =>
    {
        panel.SetTitle("URAF");
    });
}, priority: 100);
```

For simple fixed edits, use `Patch`:

```csharp
RamenTrainingDisplay.Patch(p => p
    .Training(RamenTrain.Speed)
    .AddDescription("自定义说明")
    .Border(Color.Yellow), priority: 100);

RamenTrainingDisplay.Patch(p =>
{
    p.TrainingByCommandId(105).AddMarkup("[aqua]command note[/]");
    p.Important.AddText("重要信息");
    p.Extra.AddMarkup("[blue]额外信息[/]");
    p.Scenario("uraf").Title("URAF");
});
```

`Modify` can inspect `RamenTrainingDisplayContext` and mutate the display editor directly. `Patch` records simple operations and replays them before render. Both APIs run before `RamenScenarioAnalyzer` calls `LiveDisplay.SetPanel`; they do not expose LiveDisplay or Spectre `Layout`/`Table` internals.

The shared context is required so the registering plugin and `RamenScenarioAnalyzer` use the same static modifier store.
