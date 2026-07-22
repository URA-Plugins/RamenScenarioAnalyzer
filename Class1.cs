using Gallop;
using Gallop.Endpoints;
using UmamusumeResponseAnalyzer.LiveDisplay;
using UmamusumeResponseAnalyzer.Plugin;

namespace RamenScenarioAnalyzer;

public sealed class RamenScenarioAnalyzer : IPlugin
{
    const string WorkspaceTitle = "RamenScenarioAnalyzer";
    const string TrainingPanelKey = "training";

    ILiveDisplayOutput? liveDisplay;
    LiveDisplayWorkspace? workspace;
    IDisposable? eventLoggerDisplaySubscription;
    bool checkedBootstrapWorkspace;
    int currentTurn;

    public string Name => "RamenScenarioAnalyzer";

    public string Author => "UmamusumeResponseAnalyzer";

    public string[] Targets => ["Cygames"];

    public void Initialize(IPluginContext context)
    {
        liveDisplay = context.LiveDisplay;
        workspace = LiveDisplay.CreateWorkspace(WorkspaceTitle);
        checkedBootstrapWorkspace = false;
        currentTurn = 0;
        eventLoggerDisplaySubscription?.Dispose();
        eventLoggerDisplaySubscription = RamenEventLoggerDisplay.Register();
    }

    public void Dispose()
    {
        eventLoggerDisplaySubscription?.Dispose();
        eventLoggerDisplaySubscription = null;

        if (liveDisplay is not null && workspace is not null)
            liveDisplay.RemoveWorkspace(workspace);

        workspace = null;
        liveDisplay = null;
    }

    [ResponseAnalyzer<GameApi.SingleModeRamen.ChangeShortCut>(1)]
    public ValueTask Analyze(SingleModeRamenChangeShortCutResponse response)
        => AnalyzeRamenResponse(new(
            response,
            response.data.chara_info,
            response.data.ramen_data_set,
            homeInfo: null,
            response.data.unchecked_event_array,
            commandResult: null));

    [ResponseAnalyzer<GameApi.SingleModeRamen.CheckEvent>(1)]
    public ValueTask Analyze(SingleModeRamenCheckEventResponse response)
        => AnalyzeRamenResponse(new(
            response,
            response.data.chara_info,
            response.data.ramen_data_set,
            response.data.home_info,
            response.data.unchecked_event_array,
            commandResult: null));

    [ResponseAnalyzer<GameApi.SingleModeRamen.Load>(1)]
    public ValueTask Analyze(SingleModeRamenLoadResponse response)
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

    [ResponseAnalyzer<GameApi.SingleModeRamen.CheckPoint>(1)]
    public ValueTask Analyze(SingleModeRamenCheckPointResponse response)
        => AnalyzeRamenResponse(new(
            response,
            response.data.chara_info,
            response.data.ramen_data_set,
            homeInfo: null,
            response.data.unchecked_event_array,
            commandResult: null));

    [ResponseAnalyzer<GameApi.SingleModeRamen.Continue>(1)]
    public ValueTask Analyze(SingleModeRamenContinueResponse response)
        => AnalyzeRamenResponse(new(
            response,
            response.data.chara_info,
            response.data.ramen_data_set,
            response.data.home_info,
            response.data.unchecked_event_array,
            commandResult: null));

    [ResponseAnalyzer<GameApi.SingleModeRamen.ExecCommand>(1)]
    public ValueTask Analyze(SingleModeRamenExecCommandResponse response)
        => AnalyzeRamenResponse(new(
            response,
            response.data.chara_info,
            response.data.ramen_data_set,
            response.data.home_info,
            response.data.unchecked_event_array,
            response.data.command_result));

    [ResponseAnalyzer<GameApi.SingleModeRamen.FinishClawCrane>(1)]
    public ValueTask Analyze(SingleModeRamenFinishClawCraneResponse response)
        => AnalyzeRamenResponse(new(
            response,
            response.data.chara_info,
            response.data.ramen_data_set,
            homeInfo: null,
            response.data.unchecked_event_array,
            commandResult: null));

    [ResponseAnalyzer<GameApi.SingleModeRamen.GainSkills>(1)]
    public ValueTask Analyze(SingleModeRamenGainSkillsResponse response)
        => AnalyzeRamenResponse(new(
            response,
            response.data.chara_info,
            response.data.ramen_data_set,
            response.data.home_info,
            uncheckedEventArray: null,
            commandResult: null));

    [ResponseAnalyzer<GameApi.SingleModeRamen.RaceEnd>(1)]
    public ValueTask Analyze(SingleModeRamenRaceEndResponse response)
        => AnalyzeRamenResponse(new(
            response,
            response.data.chara_info,
            response.data.ramen_data_set,
            response.data.home_info,
            uncheckedEventArray: null,
            commandResult: null));

    [ResponseAnalyzer<GameApi.SingleModeRamen.RaceEntry>(1)]
    public ValueTask Analyze(SingleModeRamenRaceEntryResponse response)
        => AnalyzeRamenResponse(new(
            response,
            response.data.chara_info,
            response.data.ramen_data_set,
            response.data.home_info,
            response.data.unchecked_event_array,
            commandResult: null));

    [ResponseAnalyzer<GameApi.SingleModeRamen.RaceOut>(1)]
    public ValueTask Analyze(SingleModeRamenRaceOutResponse response)
        => AnalyzeRamenResponse(new(
            response,
            response.data.chara_info,
            response.data.ramen_data_set,
            response.data.home_info,
            response.data.unchecked_event_array,
            commandResult: null));

    [ResponseAnalyzer<GameApi.SingleModeRamen.RamenLive>(1)]
    public ValueTask Analyze(SingleModeRamenRamenLiveResponse response)
        => AnalyzeRamenResponse(new(
            response,
            response.data.chara_info,
            response.data.ramen_data_set,
            response.data.home_info,
            response.data.unchecked_event_array,
            commandResult: null));

    [ResponseAnalyzer<GameApi.SingleModeRamen.SelectRegion>(1)]
    public ValueTask Analyze(SingleModeRamenSelectRegionResponse response)
        => AnalyzeRamenResponse(new(
            response,
            response.data.chara_info,
            response.data.ramen_data_set,
            response.data.home_info,
            response.data.unchecked_event_array,
            commandResult: null));

    [ResponseAnalyzer<GameApi.SingleModeRamen.Tasting>(1)]
    public ValueTask Analyze(SingleModeRamenTastingResponse response)
        => AnalyzeRamenResponse(new(
            response,
            response.data.chara_info,
            response.data.ramen_data_set,
            response.data.home_info,
            uncheckedEventArray: null,
            commandResult: null));

    [ResponseAnalyzer<GameApi.SingleModeRamen.UrafEffectApply>(1)]
    public ValueTask Analyze(SingleModeRamenUrafEffectApplyResponse response)
        => AnalyzeRamenResponse(new(
            response,
            response.data.chara_info,
            response.data.ramen_data_set,
            response.data.home_info,
            uncheckedEventArray: null,
            commandResult: null));

    ValueTask AnalyzeRamenResponse(RamenScenarioResponseData data)
    {
        if (!CanRenderTrainingPanel(data))
            return ValueTask.CompletedTask;

        var turn = new TurnInfoRamen(data);
        var trainStats = RamenTrainingStatsCalculator.CreateTrainStats(turn);
        var context = new RamenTrainingDisplayContext(data.Response, data, turn, trainStats);
        var builder = RamenTrainingDisplayBuilder.CreateDefault(context);
        AddTurnStateImportantRows(data, turn, builder);

        ApplyModifiers(context, builder);

        var content = RamenTrainingDisplayRenderer.Render(builder);
        SwitchFromBootstrapOnFirstActivation();
        LiveDisplay.SetPanel(Workspace, TrainingPanelKey, "拉面杯训练", content, fullBleed: true);
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
        var rows = new List<string>();
        if (currentTurn != turn.Turn - 1
            && currentTurn != turn.Turn
            && turn.Turn != 1)
        {
            rows.Add(RamenDisplayText.WrongTurnAlert(currentTurn, turn.Turn));
        }

        if (data.CharaInfo.playing_state != 1)
        {
            rows.Add(RamenDisplayText.RepeatTurn);
        }
        else
        {
            currentTurn = turn.Turn;
        }

        if (rows.Count > 0)
            builder.ImportantRows.InsertRange(0, rows);
    }

    void SwitchFromBootstrapOnFirstActivation()
    {
        if (checkedBootstrapWorkspace)
            return;

        checkedBootstrapWorkspace = true;
        if (LiveDisplay.CurrentWorkspace?.Title == "启动")
            LiveDisplay.SwitchWorkspace(Workspace);
    }

    void ApplyModifiers(RamenTrainingDisplayContext context, RamenTrainingDisplayBuilder builder)
    {
        var display = new RamenTrainingDisplayEditor(builder);
        foreach (var modifier in RamenTrainingDisplayRegistry.Snapshot())
        {
            try
            {
                modifier(context, display);
            }
            catch (Exception ex)
            {
                LiveDisplay.Log(Workspace, $"Ramen training display modifier failed: {ex.Message}", LiveDisplaySeverity.Error);
#if DEBUG
                throw;
#endif
            }
        }
    }

    ILiveDisplayOutput LiveDisplay => liveDisplay
        ?? throw new InvalidOperationException("RamenScenarioAnalyzer 尚未初始化 LiveDisplay。");

    LiveDisplayWorkspace Workspace => workspace
        ?? throw new InvalidOperationException("RamenScenarioAnalyzer 尚未创建 LiveDisplay workspace。");
}
