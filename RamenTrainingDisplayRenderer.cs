using UmamusumeResponseAnalyzer.LiveDisplay;

namespace RamenScenarioAnalyzer;

internal static class RamenTrainingDisplayRenderer
{
    public static LiveDisplayContent Render(RamenTrainingDisplayBuilder builder)
    {
        var lines = new List<string>
        {
            string.Join(" | ", builder.HeaderPanels.Select(x => x.Content)),
        };
        AppendSection(lines, "重要信息", builder.ImportantRows);
        AppendSection(lines, "剧本信息", builder.ScenarioPanels.Select(x =>
            x.ShowHeader ? $"{x.Title}: {x.Content}" : x.Content));
        lines.Add(string.Empty);
        lines.Add("== 训练信息 ==");
        if (builder.TrainingCards.Count == 0)
        {
            lines.Add("无训练信息");
        }
        else
        {
            foreach (var card in builder.TrainingCards)
            {
                lines.Add($"{(card.Highlighted ? "▶ " : string.Empty)}[{card.Title}]");
                lines.AddRange(card.Rows.Select(x => $"  {x}"));
                lines.Add(string.Empty);
            }
        }
        AppendSection(lines, "Extras", builder.ExtraRows);
        return LiveDisplayContent.Text(string.Join(Environment.NewLine, lines));
    }

    static void AppendSection(List<string> output, string title, IEnumerable<string> rows)
    {
        var values = rows.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        if (values.Length == 0)
            return;
        output.Add(string.Empty);
        output.Add($"== {title} ==");
        output.AddRange(values);
    }
}
