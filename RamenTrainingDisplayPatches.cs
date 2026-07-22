namespace RamenScenarioAnalyzer;

public sealed class RamenTrainingDisplayPatch
{
    readonly List<IRamenTrainingDisplayPatchOperation> operations = [];

    public RamenTrainingDisplayPatch()
    {
        Important = new(operations, RamenDisplayRowsTarget.Important);
        Extra = new(operations, RamenDisplayRowsTarget.Extra);
    }

    public RamenDisplayRowsPatch Important { get; }

    public RamenDisplayRowsPatch Extra { get; }

    public RamenTrainingCardPatch Training(RamenTrain train)
        => new(operations, new TrainingByTrainIndex((int)train));

    public RamenTrainingCardPatch TrainingByCommandId(int commandId)
        => new(operations, new TrainingByCommandId(commandId));

    public RamenScenarioPanelPatch Scenario(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return new(operations, key);
    }

    internal void Apply(RamenTrainingDisplayEditor display)
    {
        foreach (var operation in operations)
            operation.Apply(display.Builder);
    }
}

public sealed class RamenTrainingCardPatch
{
    readonly List<IRamenTrainingDisplayPatchOperation> operations;
    readonly IRamenTrainingCardSelector selector;

    internal RamenTrainingCardPatch(
        List<IRamenTrainingDisplayPatchOperation> operations,
        IRamenTrainingCardSelector selector)
    {
        this.operations = operations;
        this.selector = selector;
    }

    public RamenTrainingCardPatch AddDescription(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        operations.Add(new TrainingCardAddRow(selector, text));
        return this;
    }

    public RamenTrainingCardPatch Highlight()
    {
        operations.Add(new TrainingCardHighlight(selector));
        return this;
    }

    public RamenTrainingCardPatch Title(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        operations.Add(new TrainingCardSetTitle(selector, title));
        return this;
    }
}

public sealed class RamenDisplayRowsPatch
{
    readonly List<IRamenTrainingDisplayPatchOperation> operations;
    readonly RamenDisplayRowsTarget target;

    internal RamenDisplayRowsPatch(
        List<IRamenTrainingDisplayPatchOperation> operations,
        RamenDisplayRowsTarget target)
    {
        this.operations = operations;
        this.target = target;
    }

    public RamenDisplayRowsPatch AddText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        operations.Add(new RowsAddText(target, text));
        return this;
    }
}

public sealed class RamenScenarioPanelPatch
{
    readonly List<IRamenTrainingDisplayPatchOperation> operations;
    readonly string key;

    internal RamenScenarioPanelPatch(
        List<IRamenTrainingDisplayPatchOperation> operations,
        string key)
    {
        this.operations = operations;
        this.key = key;
    }

    public RamenScenarioPanelPatch AddText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        operations.Add(new ScenarioPanelAddRow(key, text));
        return this;
    }

    public RamenScenarioPanelPatch Title(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        operations.Add(new ScenarioPanelSetTitle(key, title));
        return this;
    }
}

interface IRamenTrainingDisplayPatchOperation
{
    void Apply(RamenTrainingDisplayBuilder builder);
}

interface IRamenTrainingCardSelector
{
    RamenTrainingCard Select(RamenTrainingDisplayBuilder builder);
}

sealed record TrainingByTrainIndex(int TrainIndex) : IRamenTrainingCardSelector
{
    public RamenTrainingCard Select(RamenTrainingDisplayBuilder builder)
        => builder.FindTrainingCardByTrainIndex(TrainIndex)
            ?? throw new InvalidOperationException($"拉面杯训练卡不存在: trainIndex={TrainIndex}");
}

sealed record TrainingByCommandId(int CommandId) : IRamenTrainingCardSelector
{
    public RamenTrainingCard Select(RamenTrainingDisplayBuilder builder)
        => builder.FindTrainingCardByCommandId(CommandId)
            ?? throw new InvalidOperationException($"拉面杯训练卡不存在: commandId={CommandId}");
}

sealed record TrainingCardAddRow(IRamenTrainingCardSelector Selector, string Row)
    : IRamenTrainingDisplayPatchOperation
{
    public void Apply(RamenTrainingDisplayBuilder builder)
        => Selector.Select(builder).AddRow(Row);
}

sealed record TrainingCardHighlight(IRamenTrainingCardSelector Selector)
    : IRamenTrainingDisplayPatchOperation
{
    public void Apply(RamenTrainingDisplayBuilder builder)
        => Selector.Select(builder).Highlighted = true;
}

sealed record TrainingCardSetTitle(IRamenTrainingCardSelector Selector, string Title)
    : IRamenTrainingDisplayPatchOperation
{
    public void Apply(RamenTrainingDisplayBuilder builder)
        => Selector.Select(builder).Title = Title;
}

enum RamenDisplayRowsTarget
{
    Important,
    Extra
}

sealed record RowsAddText(RamenDisplayRowsTarget Target, string Row)
    : IRamenTrainingDisplayPatchOperation
{
    public void Apply(RamenTrainingDisplayBuilder builder)
    {
        if (Target is RamenDisplayRowsTarget.Important)
            builder.ImportantRows.Add(Row);
        else
            builder.ExtraRows.Add(Row);
    }
}

sealed record ScenarioPanelAddRow(string Key, string Row) : IRamenTrainingDisplayPatchOperation
{
    public void Apply(RamenTrainingDisplayBuilder builder)
    {
        var panel = builder.FindScenarioPanel(Key)
            ?? throw new InvalidOperationException($"拉面杯剧本面板不存在: key={Key}");

        panel.Content = $"{panel.Content}{Environment.NewLine}{Row}";
    }
}

sealed record ScenarioPanelSetTitle(string Key, string Title) : IRamenTrainingDisplayPatchOperation
{
    public void Apply(RamenTrainingDisplayBuilder builder)
    {
        var panel = builder.FindScenarioPanel(Key)
            ?? throw new InvalidOperationException($"拉面杯剧本面板不存在: key={Key}");

        panel.Title = Title;
        panel.ShowHeader = true;
    }
}
