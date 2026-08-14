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

public static class RamenTrainingDisplay
{
    static readonly object CurrentGate = new();
    static readonly List<ModifierRegistration> Modifiers = [];
    static CurrentDisplay? currentDisplay;

    public static IDisposable RegisterModifier(
        Action<RamenTrainingDisplayContext, RamenTrainingDisplayEditor> modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);

        var registration = new ModifierRegistration(modifier);
        lock (CurrentGate)
            Modifiers.Add(registration);

        try
        {
            RefreshCurrent();
            return registration;
        }
        catch
        {
            registration.Remove(refresh: false);
            throw;
        }
    }

    public static bool ModifyCurrent(
        Action<RamenTrainingDisplayContext, RamenTrainingDisplayEditor> modifier,
        bool switchToWorkspace = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(modifier);

        CurrentDisplay? current;
        lock (CurrentGate)
            current = currentDisplay;

        return current is not null &&
            RenderCurrent(current, modifier, switchToWorkspace, cancellationToken);
    }

    public static bool RefreshCurrent(
        bool switchToWorkspace = false,
        CancellationToken cancellationToken = default)
    {
        CurrentDisplay? current;
        lock (CurrentGate)
            current = currentDisplay;

        return current is not null &&
            RenderCurrent(current, modifier: null, switchToWorkspace, cancellationToken);
    }

    internal static void SetCurrentDisplay(
        object owner,
        Func<
            Action<RamenTrainingDisplayContext, RamenTrainingDisplayEditor>?,
            bool,
            Func<bool>,
            bool> render)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(render);

        var current = new CurrentDisplay(owner, render);
        CurrentDisplay? previous;
        lock (CurrentGate)
        {
            previous = currentDisplay;
            currentDisplay = current;
        }

        try
        {
            _ = RenderCurrent(current, modifier: null, switchToWorkspace: true, CancellationToken.None);
        }
        catch
        {
            lock (CurrentGate)
                if (ReferenceEquals(currentDisplay, current))
                    currentDisplay = previous;
            throw;
        }
    }

    internal static void ClearCurrentDisplay(object owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        lock (CurrentGate)
            if (ReferenceEquals(currentDisplay?.Owner, owner))
                currentDisplay = null;
    }

    internal static RamenDisplayLine CreateStyledLine(RamenDisplaySegment[] segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (segments.Any(segment => segment.Text is null))
            throw new ArgumentException("显示片段文本不能为 null。", nameof(segments));
        return RamenDisplayLine.Styled(segments);
    }

    static bool RenderCurrent(
        CurrentDisplay current,
        Action<RamenTrainingDisplayContext, RamenTrainingDisplayEditor>? modifier,
        bool switchToWorkspace,
        CancellationToken cancellationToken)
    {
        ModifierRegistration[] modifiers;
        lock (CurrentGate)
            modifiers = [.. Modifiers];

        return current.Render(
            (context, editor) =>
            {
                foreach (var registration in modifiers)
                    registration.Apply(context, editor);
                modifier?.Invoke(context, editor);
            },
            switchToWorkspace,
            () => !cancellationToken.IsCancellationRequested && IsCurrent(current));
    }

    static bool IsCurrent(CurrentDisplay candidate)
    {
        lock (CurrentGate)
            return ReferenceEquals(currentDisplay, candidate);
    }

    sealed record CurrentDisplay(
        object Owner,
        Func<
            Action<RamenTrainingDisplayContext, RamenTrainingDisplayEditor>?,
            bool,
            Func<bool>,
            bool> Render);

    sealed class ModifierRegistration(
        Action<RamenTrainingDisplayContext, RamenTrainingDisplayEditor> modifier) : IDisposable
    {
        int disposed;

        internal void Apply(RamenTrainingDisplayContext context, RamenTrainingDisplayEditor editor)
            => modifier(context, editor);

        public void Dispose() => Remove(refresh: true);

        internal void Remove(bool refresh)
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;

            lock (CurrentGate)
                Modifiers.Remove(this);
            if (refresh)
                RefreshCurrent();
        }
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
