namespace RamenScenarioAnalyzer;

public enum RamenTrain
{
    Speed = 1,
    Stamina = 2,
    Power = 3,
    Guts = 4,
    Wiz = 5,
    Wisdom = 5
}

public readonly record struct RamenTrainingDisplayId(int SingleModeCharaId, int Turn);

public static class RamenTrainingDisplay
{
    static readonly object Gate = new();
    static readonly Dictionary<RamenTrainingDisplayId, DisplayUnit> Units = [];
    static long nextProducerSequence;

    public static RamenTrainingDisplayPartProducer RegisterPartProducer()
        => new(Interlocked.Increment(ref nextProducerSequence));

    internal static void Update(
        object owner,
        RamenTrainingDisplayId id,
        RamenTrainingDisplayContext context,
        Func<RamenTrainingDisplayContext, RamenTrainingDisplayBuilder> createBuilder,
        Action<RamenTrainingDisplayId, UmamusumeResponseAnalyzer.TerminalGui.WorkspaceContent, bool> publish)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(createBuilder);
        ArgumentNullException.ThrowIfNull(publish);

        lock (Gate)
        {
            if (!Units.TryGetValue(id, out var unit))
                Units.Add(id, unit = new());
            unit.Scenario = new(owner, context, createBuilder, publish);
        }
    }

    public static bool Show(
        RamenTrainingDisplayId id,
        bool switchToWorkspace = false,
        CancellationToken cancellationToken = default)
    {
        ScenarioPart scenario;
        Action<RamenTrainingDisplayContext, RamenTrainingDisplayEditor>[] parts;
        lock (Gate)
        {
            if (!Units.TryGetValue(id, out var unit) || unit.Scenario is not { } value)
                return false;
            scenario = value;
            parts = [.. unit.Parts
                .OrderBy(entry => entry.Key.Sequence)
                .Select(entry => entry.Value)];
        }

        var builder = scenario.CreateBuilder(scenario.Context);
        var editor = new RamenTrainingDisplayEditor(builder);
        foreach (var part in parts)
            part(scenario.Context, editor);
        var content = RamenTrainingDisplayRenderer.Render(RamenDisplaySnapshot.Create(builder));
        if (cancellationToken.IsCancellationRequested)
            return false;
        scenario.Publish(id, content, switchToWorkspace);
        return true;
    }

    internal static void Remove(object owner, RamenTrainingDisplayId id)
    {
        ArgumentNullException.ThrowIfNull(owner);
        lock (Gate)
            if (Units.TryGetValue(id, out var unit) && ReferenceEquals(unit.Scenario?.Owner, owner))
                Units.Remove(id);
    }

    internal static void Clear(object owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        lock (Gate)
            foreach (var id in Units
                         .Where(entry => ReferenceEquals(entry.Value.Scenario?.Owner, owner))
                         .Select(entry => entry.Key)
                         .ToArray())
                Units.Remove(id);
    }

    internal static void UpdatePart(
        RamenTrainingDisplayPartProducer producer,
        RamenTrainingDisplayId id,
        Action<RamenTrainingDisplayContext, RamenTrainingDisplayEditor> part)
    {
        ArgumentNullException.ThrowIfNull(part);
        lock (Gate)
        {
            ObjectDisposedException.ThrowIf(producer.IsDisposed, producer);
            if (!Units.TryGetValue(id, out var unit))
                Units.Add(id, unit = new());
            unit.Parts[producer] = part;
        }
    }

    internal static void RemoveProducer(RamenTrainingDisplayPartProducer producer)
    {
        lock (Gate)
        {
            foreach (var (id, unit) in Units.ToArray())
            {
                unit.Parts.Remove(producer);
                if (unit.Scenario is null && unit.Parts.Count == 0)
                    Units.Remove(id);
            }
        }
    }

    internal static RamenDisplayLine CreateStyledLine(RamenDisplaySegment[] segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (segments.Any(segment => segment.Text is null))
            throw new ArgumentException("显示片段文本不能为 null。", nameof(segments));
        return RamenDisplayLine.Styled(segments);
    }

    sealed record ScenarioPart(
        object Owner,
        RamenTrainingDisplayContext Context,
        Func<RamenTrainingDisplayContext, RamenTrainingDisplayBuilder> CreateBuilder,
        Action<RamenTrainingDisplayId, UmamusumeResponseAnalyzer.TerminalGui.WorkspaceContent, bool> Publish);

    sealed class DisplayUnit
    {
        internal ScenarioPart? Scenario { get; set; }
        internal Dictionary<RamenTrainingDisplayPartProducer, Action<RamenTrainingDisplayContext, RamenTrainingDisplayEditor>> Parts { get; } = [];
    }
}

public sealed class RamenTrainingDisplayPartProducer : IDisposable
{
    int disposed;

    internal RamenTrainingDisplayPartProducer(long sequence)
    {
        Sequence = sequence;
    }

    internal long Sequence { get; }
    internal bool IsDisposed => Volatile.Read(ref disposed) != 0;

    public void Update(
        RamenTrainingDisplayId id,
        Action<RamenTrainingDisplayContext, RamenTrainingDisplayEditor> part)
        => RamenTrainingDisplay.UpdatePart(this, id, part);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
            RamenTrainingDisplay.RemoveProducer(this);
    }
}

public sealed class RamenTrainingDisplayEditor
{
    internal RamenTrainingDisplayEditor(RamenTrainingDisplayBuilder builder)
    {
        Training = new(builder);
        Important = new(builder.ImportantRows);
        Extra = new(builder.ExtraRows);
        Scenario = new(builder);
    }

    public RamenTrainingCardsEditor Training { get; }
    public RamenDisplayRowsEditor Important { get; }
    public RamenDisplayRowsEditor Extra { get; }
    public RamenScenarioPanelsEditor Scenario { get; }
}

public sealed class RamenTrainingCardsEditor
{
    readonly RamenTrainingDisplayBuilder builder;

    internal RamenTrainingCardsEditor(RamenTrainingDisplayBuilder builder)
    {
        this.builder = builder;
    }

    public RamenTrainingCardEditor Get(RamenTrain train)
        => new(builder.FindTrainingCardByTrainIndex((int)train)
            ?? throw new InvalidOperationException($"拉面杯训练卡不存在: {train}"));

    public RamenTrainingCardEditor GetByCommandId(int commandId)
        => new(builder.FindTrainingCardByCommandId(commandId)
            ?? throw new InvalidOperationException($"拉面杯训练 command 不存在: {commandId}"));

    public void Modify(RamenTrain train, Action<RamenTrainingCardEditor> modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        modifier(Get(train));
    }

    public void ModifyByCommandId(int commandId, Action<RamenTrainingCardEditor> modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        modifier(GetByCommandId(commandId));
    }
}

public sealed class RamenTrainingCardEditor
{
    readonly RamenTrainingCard card;

    internal RamenTrainingCardEditor(RamenTrainingCard card)
    {
        this.card = card;
    }

    public int CommandId => card.CommandId;
    public RamenTrain Train => (RamenTrain)card.TrainIndex;

    public string Title
    {
        get => card.Title;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            card.Title = value;
        }
    }

    public bool Highlighted
    {
        get => card.Highlighted;
        set => card.Highlighted = value;
    }

    public void SetTitle(string title) => Title = title;
    public void Highlight() => Highlighted = true;
    public void AddDescription(string text) => AddText(text);

    public void AddText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        card.AddRow(text);
    }

    public void AddStyled(params RamenDisplaySegment[] segments)
        => card.AddRow(RamenTrainingDisplay.CreateStyledLine(segments));
}

public sealed class RamenDisplayRowsEditor
{
    readonly RamenDisplayRows rows;

    internal RamenDisplayRowsEditor(RamenDisplayRows rows)
    {
        this.rows = rows;
    }

    public void AddText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        rows.Add(text);
    }

    public void AddStyled(params RamenDisplaySegment[] segments)
        => rows.Add(RamenTrainingDisplay.CreateStyledLine(segments));
}

public sealed class RamenScenarioPanelsEditor
{
    readonly RamenTrainingDisplayBuilder builder;

    internal RamenScenarioPanelsEditor(RamenTrainingDisplayBuilder builder)
    {
        this.builder = builder;
    }

    public RamenDisplayPanelEditor Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return new(builder.FindScenarioPanel(key)
            ?? throw new InvalidOperationException($"拉面杯剧本面板不存在: key={key}"));
    }

    public RamenDisplayPanelEditor Add(string key, string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(title);
        if (builder.FindScenarioPanel(key) is not null)
            throw new InvalidOperationException($"拉面杯剧本面板已存在: key={key}");

        var panel = new RamenDisplayPanel(key, title, string.Empty, showHeader: true);
        builder.ScenarioPanels.Add(panel);
        return new(panel);
    }

    public void Modify(string key, Action<RamenDisplayPanelEditor> modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        modifier(Get(key));
    }
}

public sealed class RamenDisplayPanelEditor
{
    readonly RamenDisplayPanel panel;

    internal RamenDisplayPanelEditor(RamenDisplayPanel panel)
    {
        this.panel = panel;
    }

    public string Key => panel.Key;

    public string Title
    {
        get => panel.Title;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            panel.Title = value;
            panel.ShowHeader = true;
        }
    }

    public void SetTitle(string title) => Title = title;

    public void SetDescription(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        panel.Content = text;
    }

    public void AddText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        panel.AddRow(text);
    }

    public void AddStyled(params RamenDisplaySegment[] segments)
        => panel.AddRow(RamenTrainingDisplay.CreateStyledLine(segments));
}
