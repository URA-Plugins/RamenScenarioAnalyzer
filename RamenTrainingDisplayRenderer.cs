using Spectre.Console;
using Spectre.Console.Rendering;

namespace RamenScenarioAnalyzer;

internal static class RamenTrainingDisplayRenderer
{
    public static IRenderable Render(RamenTrainingDisplayBuilder builder)
    {
        var layout = new Layout().SplitColumns(
            new Layout("Main").Size(CommandInfoLayout.Current.MainSectionWidth).SplitRows(
                BuildPanelRow("体力干劲条", builder.HeaderPanels, showHeaders: false).Size(3),
                new Layout("重要信息").Size(5),
                BuildPanelRow("剧本信息", builder.ScenarioPanels, showHeaders: true).Size(3),
                new Layout("训练信息")).Ratio(4),
            new Layout("Ext").Ratio(1));

        layout["重要信息"].Update(new Panel(BuildRows(builder.ImportantRows)).Expand());
        layout["训练信息"].Update(BuildTrainingGrid(builder.TrainingCards));
        layout["Ext"].Update(BuildExtraTable(builder.ExtraRows));
        return layout;
    }

    static Layout BuildPanelRow(
        string name,
        IReadOnlyList<RamenDisplayPanel> panels,
        bool showHeaders)
    {
        var row = new Layout(name);
        var children = panels.Count == 0
            ? [new Layout("empty").Ratio(1)]
            : panels.Select(x => new Layout(x.Key).Ratio(x.Ratio)).ToArray();
        row.SplitColumns(children);

        foreach (var panel in panels)
        {
            var panelView = new Panel(panel.Content).Expand();
            if (showHeaders && panel.ShowHeader)
                panelView.Header(panel.Title);
            row[panel.Key].Update(panelView);
        }

        return row;
    }

    static IRenderable BuildRows(IReadOnlyList<IRenderable> rows)
    {
        if (rows.Count == 0)
            return new Text(string.Empty);

        var table = new Table();
        table.HideHeaders();
        table.NoBorder();
        table.AddColumn(string.Empty);
        foreach (var row in rows)
            table.AddRow(row);
        return table;
    }

    static IRenderable BuildTrainingGrid(IReadOnlyList<RamenTrainingCard> cards)
    {
        if (cards.Count == 0)
            return new Text("无训练信息");

        var grid = new Grid();
        grid.AddColumns(Math.Max(6, cards.Count));
        foreach (var column in grid.Columns)
            column.Padding = new Padding(0, 0, 0, 0);

        grid.AddRow([.. cards.Select(x => new Padder(BuildTrainingTable(x)).Padding(0, 0, 0, 0))]);
        return grid;
    }

    static Table BuildTrainingTable(RamenTrainingCard card)
    {
        var table = new Table().AddColumn(card.Title);
        foreach (var row in card.Rows)
            table.AddRow(row);

        if (card.BorderColor is { } borderColor)
            table.BorderColor(borderColor);

        return table;
    }

    static IRenderable BuildExtraTable(IReadOnlyList<IRenderable> rows)
    {
        var table = new Table().AddColumn("Extras");
        table.HideHeaders();
        foreach (var row in rows)
            table.AddRow(row);
        return table;
    }
}
