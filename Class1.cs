using Gallop;
using UmamusumeResponseAnalyzer.TerminalGui;
using UmamusumeResponseAnalyzer.Plugin;

namespace RamenScenarioAnalyzer;

public sealed class RamenScenarioAnalyzer : IPlugin
{
    const string WorkspaceTitle = "RamenScenarioAnalyzer";
    const string TrainingPanelKey = "training";
    const string CommonResponseEndpoints =
        "^/umamusume/single_mode_ramen/(?:change_short_cut|check_event|check_point|continue|exec_command|finish_claw_crane|gain_skills|race_end|race_entry|race_out|ramen_live|select_region|tasting|uraf_effect_apply)$";

    readonly object stateGate = new();
    readonly RamenDisplayHistory history = new(WorkspaceTitle, TrainingPanelKey, "拉面杯训练");
    Workspace? workspace;
    int currentTurn;

    public void Initialize(IPluginContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        history.Initialize(context.Application);
        context.Analyzers.Register<SingleModeRamenExecCommandResponse>(
            AnalyzerKind.Response,
            [EndpointPattern.Regex(CommonResponseEndpoints)],
            invocation => Analyze(invocation.Payload),
            priority: 1);
        context.Analyzers.Register<SingleModeRamenLoadResponse>(
            AnalyzerKind.Response,
            [EndpointPattern.Exact("/umamusume/single_mode_ramen/load")],
            invocation => Analyze(invocation.Payload),
            priority: 1);
    }

    public void Dispose()
    {
        history.Stop();
        lock (stateGate)
        {
            workspace?.RemovePanel(TrainingPanelKey);
            workspace = null;
        }
    }

    public Task ConfigPromptAsync(
        Terminal.Gui.App.IApplication application,
        CancellationToken cancellationToken = default)
        => history.ConfigPromptAsync(application, cancellationToken);

    ValueTask Analyze(SingleModeRamenLoadResponse response)
    {
        var loadCommon = response.data.single_mode_load_common;
        return AnalyzeRamenResponse(new(
            response,
            loadCommon.chara_info,
            response.data.ramen_data_set,
            loadCommon.home_info,
            loadCommon.unchecked_event_array,
            commandResult: null));
    }

    ValueTask Analyze(SingleModeRamenExecCommandResponse response)
        => AnalyzeRamenResponse(new(
            response,
            response.data.chara_info,
            response.data.ramen_data_set,
            response.data.home_info,
            response.data.unchecked_event_array,
            response.data.command_result));

    ValueTask AnalyzeRamenResponse(RamenScenarioResponseData data)
    {
        lock (stateGate)
        {
            if (!CanRenderTrainingPanel(data))
                return ValueTask.CompletedTask;

            var turn = new TurnInfoRamen(data);
            var trainStats = RamenTrainingStatsCalculator.CreateTrainStats(turn);
            var context = new RamenTrainingDisplayContext(data.Response, data, turn, trainStats);
            var builder = RamenTrainingDisplayBuilder.CreateDefault(context);
            AddTurnStateImportantRows(data, turn, builder);
            RamenEventLoggerDisplay.Apply(context, builder);
            var snapshot = RamenDisplaySnapshot.Create(builder);
            var target = Workspace.Create(WorkspaceTitle);
            history.Publish(
                target,
                new(data.CharaInfo.single_mode_chara_id, data.CharaInfo.turn),
                RamenTrainingDisplayRenderer.Render(snapshot),
                switchToWorkspace: true,
                () => workspace = target);
        }

        return ValueTask.CompletedTask;
    }

    static bool CanRenderTrainingPanel(RamenScenarioResponseData data)
    {
        if (data.CharaInfo.state is 2 or 3)
            return false;
        if (data.UncheckedEventArray is { Length: > 0 })
            return false;
        if (data.HomeInfo?.command_info_array is not { } commands)
            return false;
        if (data.DataSet.command_info_array is null)
            return false;

        return TurnInfoRamen.BaseTrainIds.All(trainId =>
            commands.Any(command =>
                TurnInfoRamen.ToTrainId.TryGetValue(command.command_id, out var baseTrainId)
                && baseTrainId == trainId));
    }

    void AddTurnStateImportantRows(
        RamenScenarioResponseData data,
        TurnInfoRamen turn,
        RamenTrainingDisplayBuilder builder)
    {
        var rows = new List<RamenDisplayLine>();
        if (currentTurn != turn.Turn - 1
            && currentTurn != turn.Turn
            && turn.Turn != 1)
        {
            rows.Add(RamenDisplayLine.Colored(
                RamenDisplayText.WrongTurnAlert(currentTurn, turn.Turn),
                RamenDisplayColor.Red));
        }

        if (data.CharaInfo.playing_state != 1)
        {
            rows.Add(RamenDisplayLine.Colored(RamenDisplayText.RepeatTurn, RamenDisplayColor.Yellow));
        }
        else
        {
            currentTurn = turn.Turn;
        }

        if (rows.Count > 0)
            builder.ImportantRows.InsertRange(0, rows);
    }
}
