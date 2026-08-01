using Gallop;
using Gallop.Endpoints;
using UmamusumeResponseAnalyzer.TerminalGui;
using UmamusumeResponseAnalyzer.Plugin;

namespace RamenScenarioAnalyzer;

public sealed class RamenScenarioAnalyzer : IPlugin
{
    const string WorkspaceTitle = "RamenScenarioAnalyzer";
    const string TrainingPanelKey = "training";

    readonly object lifecycleGate = new();
    readonly Dictionary<int, int> publishingThreads = [];
    Workspace? workspace;
    IDisposable? eventLoggerDisplaySubscription;
    long generation;
    int publishing;
    bool accepting;
    bool starting;
    bool disposed;
    bool trainingPublished;
    bool cleanupPending;
    bool cleanupInProgress;
    bool checkedBootstrapWorkspace;
    int currentTurn;

    public string Name => "RamenScenarioAnalyzer";

    public string Author => "UmamusumeResponseAnalyzer";

    public string[] Targets => ["Cygames"];

    public void Initialize(IPluginContext context)
    {
        long initializationTicket;
        lock (lifecycleGate)
        {
            if (disposed)
                throw new InvalidOperationException("RamenScenarioAnalyzer 已释放，不能重新初始化。");
            if (starting || accepting || eventLoggerDisplaySubscription is not null)
                throw new InvalidOperationException("RamenScenarioAnalyzer 不允许重复或并发初始化。");

            starting = true;
            initializationTicket = ++generation;
        }

        IDisposable subscription;
        try
        {
            subscription = RamenEventLoggerDisplay.Register();
        }
        catch
        {
            lock (lifecycleGate)
            {
                if (starting && generation == initializationTicket)
                    starting = false;
                Monitor.PulseAll(lifecycleGate);
            }
            throw;
        }

        var installed = false;
        lock (lifecycleGate)
        {
            if (starting && generation == initializationTicket)
            {
                if (!disposed)
                {
                    checkedBootstrapWorkspace = false;
                    currentTurn = 0;
                    eventLoggerDisplaySubscription = subscription;
                    accepting = true;
                    installed = true;
                }

                starting = false;
            }
            Monitor.PulseAll(lifecycleGate);
        }

        if (!installed)
            subscription.Dispose();
    }

    public void Dispose()
    {
        IDisposable? subscription;
        lock (lifecycleGate)
        {
            disposed = true;
            accepting = false;
            generation++;
            starting = false;
            subscription = eventLoggerDisplaySubscription;
            eventLoggerDisplaySubscription = null;
            Monitor.PulseAll(lifecycleGate);
        }

        subscription?.Dispose();
        FinishDispose();
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
        long activeGeneration;
        lock (lifecycleGate)
        {
            if (!accepting)
                return ValueTask.CompletedTask;
            activeGeneration = generation;
        }

        if (!CanRenderTrainingPanel(data))
            return ValueTask.CompletedTask;

        var turn = new TurnInfoRamen(data);
        var trainStats = RamenTrainingStatsCalculator.CreateTrainStats(turn);
        var context = new RamenTrainingDisplayContext(data.Response, data, turn, trainStats);
        var builder = RamenTrainingDisplayBuilder.CreateDefault(context);
        AddTurnStateImportantRows(data, turn, builder);

        ApplyModifiers(activeGeneration, context, builder);

        var content = RamenTrainingDisplayRenderer.Render(builder);
        PublishTrainingPanel(activeGeneration, content);
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

    void ApplyModifiers(
        long activeGeneration,
        RamenTrainingDisplayContext context,
        RamenTrainingDisplayBuilder builder)
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
                LogModifierFailure(activeGeneration, ex);
#if DEBUG
                throw;
#endif
            }
        }
    }

    void LogModifierFailure(long activeGeneration, Exception exception)
    {
        if (!TryBeginPublishing(activeGeneration, includesPanel: false, out _))
            return;

        try
        {
            DisplayWorkspace.Log(
                $"Ramen training display modifier failed: {exception.Message}",
                UiSeverity.Error);
        }
        finally
        {
            EndPublishing(panelPublished: false);
        }
    }

    void PublishTrainingPanel(long activeGeneration, WorkspaceContent content)
    {
        if (!TryBeginPublishing(activeGeneration, includesPanel: true, out var switchFromBootstrap))
            return;

        var panelPublished = false;
        try
        {
            var target = DisplayWorkspace;
            if (switchFromBootstrap && Workspace.Current?.Title == "启动")
                target.SwitchTo();

            target.SetPanel(TrainingPanelKey, "拉面杯训练", content, fullBleed: true);
            panelPublished = true;
        }
        finally
        {
            EndPublishing(panelPublished);
        }
    }

    bool TryBeginPublishing(
        long activeGeneration,
        bool includesPanel,
        out bool switchFromBootstrap)
    {
        lock (lifecycleGate)
        {
            if (!accepting || generation != activeGeneration)
            {
                switchFromBootstrap = false;
                return false;
            }

            publishing++;
            var threadId = Environment.CurrentManagedThreadId;
            publishingThreads[threadId] = publishingThreads.GetValueOrDefault(threadId) + 1;

            switchFromBootstrap = includesPanel && !checkedBootstrapWorkspace;
            if (switchFromBootstrap)
                checkedBootstrapWorkspace = true;
            return true;
        }
    }

    void EndPublishing(bool panelPublished)
    {
        Workspace? cleanupTarget = null;
        lock (lifecycleGate)
        {
            if (panelPublished)
                trainingPublished = true;

            var threadId = Environment.CurrentManagedThreadId;
            if (publishingThreads[threadId] == 1)
                publishingThreads.Remove(threadId);
            else
                publishingThreads[threadId]--;

            publishing--;
            if (publishing == 0)
            {
                Monitor.PulseAll(lifecycleGate);
                if (cleanupPending)
                    cleanupTarget = TakeCleanupLocked();
            }
        }

        if (cleanupTarget is not null)
            RemoveTrainingPanel(cleanupTarget);
    }

    void FinishDispose()
    {
        Workspace? cleanupTarget;
        var threadId = Environment.CurrentManagedThreadId;
        lock (lifecycleGate)
        {
            if (publishingThreads.ContainsKey(threadId))
            {
                cleanupPending = true;
                return;
            }

            while (publishing > 0)
                Monitor.Wait(lifecycleGate);
            cleanupTarget = TakeCleanupLocked();
        }

        if (cleanupTarget is not null)
            RemoveTrainingPanel(cleanupTarget);
    }

    Workspace? TakeCleanupLocked()
    {
        cleanupPending = false;
        if (!trainingPublished || cleanupInProgress)
            return null;

        cleanupInProgress = true;
        return workspace!;
    }

    void RemoveTrainingPanel(Workspace target)
    {
        try
        {
            target.RemovePanel(TrainingPanelKey);
        }
        catch
        {
            lock (lifecycleGate)
                cleanupInProgress = false;
            throw;
        }

        lock (lifecycleGate)
        {
            trainingPublished = false;
            cleanupInProgress = false;
        }
    }

    Workspace DisplayWorkspace => workspace
        ??= Workspace.Create(WorkspaceTitle);
}
