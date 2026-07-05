using Spectre.Console;
using Spectre.Console.Rendering;

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
    public static IDisposable Modify(
        Action<RamenTrainingDisplayContext, RamenTrainingDisplayEditor> modifier,
        int priority = 0)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        return RamenTrainingDisplayRegistry.Register(modifier, priority);
    }

    public static IDisposable Patch(Action<RamenTrainingDisplayPatch> patch, int priority = 0)
    {
        ArgumentNullException.ThrowIfNull(patch);

        var displayPatch = new RamenTrainingDisplayPatch();
        patch(displayPatch);
        return Modify((_, display) => displayPatch.Apply(display), priority);
    }
}

public sealed class RamenTrainingDisplayEditor
{
    readonly RamenTrainingDisplayBuilder builder;

    internal RamenTrainingDisplayEditor(RamenTrainingDisplayBuilder builder)
    {
        this.builder = builder;
        Training = new(builder);
        Important = new(builder.ImportantRows);
        Extra = new(builder.ExtraRows);
        Scenario = new(builder);
    }

    public RamenTrainingCardsEditor Training { get; }
    public RamenDisplayRowsEditor Important { get; }
    public RamenDisplayRowsEditor Extra { get; }
    public RamenScenarioPanelsEditor Scenario { get; }

    internal RamenTrainingDisplayBuilder Builder => builder;
}

public sealed class RamenTrainingCardsEditor
{
    readonly RamenTrainingDisplayBuilder builder;

    internal RamenTrainingCardsEditor(RamenTrainingDisplayBuilder builder)
    {
        this.builder = builder;
    }

    public RamenTrainingCardEditor Get(RamenTrain train)
    {
        var card = builder.FindTrainingCardByTrainIndex((int)train)
            ?? throw new InvalidOperationException($"拉面杯训练卡不存在: {train}");
        return new(card);
    }

    public RamenTrainingCardEditor GetByCommandId(int commandId)
    {
        var card = builder.FindTrainingCardByCommandId(commandId)
            ?? throw new InvalidOperationException($"拉面杯训练 command 不存在: {commandId}");
        return new(card);
    }

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

    public Color? BorderColor
    {
        get => card.BorderColor;
        set => card.BorderColor = value;
    }

    public void SetTitle(string title) => Title = title;

    public void SetBorder(Color color) => BorderColor = color;

    public void AddDescription(string text) => AddText(text);

    public void AddText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        AddRow(new Text(text));
    }

    public void AddMarkup(string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);
        AddRow(new Markup(markup));
    }

    public void AddRow(IRenderable row)
    {
        ArgumentNullException.ThrowIfNull(row);
        card.AddRow(row);
    }
}

public sealed class RamenDisplayRowsEditor
{
    readonly List<IRenderable> rows;

    internal RamenDisplayRowsEditor(List<IRenderable> rows)
    {
        this.rows = rows;
    }

    public void AddText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        AddRow(new Text(text));
    }

    public void AddMarkup(string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);
        AddRow(new Markup(markup));
    }

    public void AddRow(IRenderable row)
    {
        ArgumentNullException.ThrowIfNull(row);
        rows.Add(row);
    }
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

        var panel = builder.FindScenarioPanel(key)
            ?? throw new InvalidOperationException($"拉面杯剧本面板不存在: key={key}");
        return new(panel);
    }

    public RamenDisplayPanelEditor Add(string key, string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(title);

        if (builder.FindScenarioPanel(key) is not null)
            throw new InvalidOperationException($"拉面杯剧本面板已存在: key={key}");

        var panel = new RamenDisplayPanel(key, title, new Text(string.Empty), showHeader: true);
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
        panel.Content = new Text(text);
    }

    public void SetMarkup(string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);
        panel.Content = new Markup(markup);
    }

    public void AddText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        AddRow(new Text(text));
    }

    public void AddMarkup(string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);
        AddRow(new Markup(markup));
    }

    public void AddRow(IRenderable row)
    {
        ArgumentNullException.ThrowIfNull(row);
        panel.Content = AppendRow(panel.Content, row);
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
