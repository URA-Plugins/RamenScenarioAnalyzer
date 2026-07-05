using Spectre.Console;
using Spectre.Console.Rendering;

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
        operations.Add(new TrainingCardAddRow(selector, new Text(text)));
        return this;
    }

    public RamenTrainingCardPatch AddMarkup(string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);
        operations.Add(new TrainingCardAddRow(selector, new Markup(markup)));
        return this;
    }

    public RamenTrainingCardPatch Border(Color color)
    {
        operations.Add(new TrainingCardSetBorder(selector, color));
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
        operations.Add(new RowsAddRenderable(target, new Text(text)));
        return this;
    }

    public RamenDisplayRowsPatch AddMarkup(string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);
        operations.Add(new RowsAddRenderable(target, new Markup(markup)));
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
        operations.Add(new ScenarioPanelAddRow(key, new Text(text)));
        return this;
    }

    public RamenScenarioPanelPatch AddMarkup(string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);
        operations.Add(new ScenarioPanelAddRow(key, new Markup(markup)));
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

sealed record TrainingCardAddRow(IRamenTrainingCardSelector Selector, IRenderable Row)
    : IRamenTrainingDisplayPatchOperation
{
    public void Apply(RamenTrainingDisplayBuilder builder)
        => Selector.Select(builder).AddRow(Row);
}

sealed record TrainingCardSetBorder(IRamenTrainingCardSelector Selector, Color Color)
    : IRamenTrainingDisplayPatchOperation
{
    public void Apply(RamenTrainingDisplayBuilder builder)
        => Selector.Select(builder).BorderColor = Color;
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

sealed record RowsAddRenderable(RamenDisplayRowsTarget Target, IRenderable Row)
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

sealed record ScenarioPanelAddRow(string Key, IRenderable Row) : IRamenTrainingDisplayPatchOperation
{
    public void Apply(RamenTrainingDisplayBuilder builder)
    {
        var panel = builder.FindScenarioPanel(Key)
            ?? throw new InvalidOperationException($"拉面杯剧本面板不存在: key={Key}");

        panel.Content = AppendRow(panel.Content, Row);
    }

    static IRenderable AppendRow(IRenderable current, IRenderable row)
    {
        var table = new Table();
        table.HideHeaders();
        table.NoBorder();
        table.AddColumn(string.Empty);
        table.AddRow(current);
        table.AddRow(row);
        return table;
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
