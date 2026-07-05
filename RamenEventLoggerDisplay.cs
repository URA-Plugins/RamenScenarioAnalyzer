using EventLoggerPlugin;
using UmamusumeResponseAnalyzer.Plugin;

[assembly: SharedContextWith("EventLoggerPlugin")]

namespace RamenScenarioAnalyzer;

internal static class RamenEventLoggerDisplay
{
    public static IDisposable Register()
        => RamenTrainingDisplay.Modify(Apply, priority: 50);

    internal static void Apply(RamenTrainingDisplayContext context, RamenTrainingDisplayEditor display)
    {
        var snapshot = EventLoggerDisplaySource.Current;
        if (!snapshot.HasData)
            return;

        var hasCurrentTrainingHistory = HasCurrentTrainingHistory(snapshot, context);
        foreach (var row in ImportantRows(snapshot, hasCurrentTrainingHistory))
            display.Important.AddMarkup(row);

        foreach (var row in ExtraRows(snapshot, hasCurrentTrainingHistory))
            display.Extra.AddMarkup(row);
    }

    static IEnumerable<string> ImportantRows(EventLoggerDisplaySnapshot snapshot, bool hasCurrentTrainingHistory)
    {
        if (snapshot.CardEvents.Appeared > 0)
        {
            yield return snapshot.CardEvents.Finished >= 5
                ? "[green]连续事件全部完成[/]"
                : $"连续事件: 出现[yellow]{snapshot.CardEvents.Appeared}[/]次, " +
                  $"走完[yellow]{snapshot.CardEvents.Finished}[/]张, " +
                  $"剩余[yellow]{snapshot.CardEvents.Remaining}[/]个";
        }

        if (hasCurrentTrainingHistory && snapshot.TrainingFailures is { } failures)
        {
            yield return $"训练赌博: [yellow]{failures.GambleTimes}[/]次, " +
                         $"失败[yellow]{failures.FailureTimes}[/]次, " +
                         $"总失败率[yellow]{failures.TotalFailureRate}[/]%";
        }
    }

    static IEnumerable<string> ExtraRows(EventLoggerDisplaySnapshot snapshot, bool hasCurrentTrainingHistory)
    {
        if (snapshot.EventCount > 0)
            yield return $"事件数: [yellow]{snapshot.EventCount}[/]";

        if (snapshot.SuccessEvents.Appeared > 0)
        {
            yield return $"赌狗事件: [yellow]{snapshot.SuccessEvents.Appeared}[/]次, " +
                         $"选择[yellow]{snapshot.SuccessEvents.Selected}[/]次, " +
                         $"成功[yellow]{snapshot.SuccessEvents.Succeeded}[/]次";
        }

        if (hasCurrentTrainingHistory && snapshot.ScenarioFriend is { } friend)
        {
            yield return $"{friend.Label}: 点击[aqua]{friend.ClickedTimes}[/]次, " +
                         $"启动[aqua]{friend.ActivatedTimes}[/]次";
        }

        if (snapshot.InheritStats.Count > 0)
            yield return $"继承属性: [cyan]{string.Join('+', snapshot.InheritStats)}[/]";

        if (snapshot.RaceWinCount > 0)
            yield return $"胜场: [yellow]{snapshot.RaceWinCount}[/]";
    }

    static bool HasCurrentTrainingHistory(EventLoggerDisplaySnapshot snapshot, RamenTrainingDisplayContext context)
        => snapshot.CurrentTurn == context.Turn.Turn;
}
