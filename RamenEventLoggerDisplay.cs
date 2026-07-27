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
            display.Important.AddRow(row);

        foreach (var row in ExtraRows(snapshot, hasCurrentTrainingHistory))
            display.Extra.AddRow(row);
    }

    static IEnumerable<RamenDisplayLine> ImportantRows(
        EventLoggerDisplaySnapshot snapshot,
        bool hasCurrentTrainingHistory)
    {
        if (snapshot.CardEvents.Appeared > 0)
        {
            yield return snapshot.CardEvents.Finished >= 5
                ? RamenDisplayLine.Colored("连续事件全部完成", RamenDisplayColor.Green)
                : RamenDisplayLine.Styled(
                    new("连续事件: 出现"),
                    new(snapshot.CardEvents.Appeared.ToString(), RamenDisplayColor.Yellow),
                    new("次, 走完"),
                    new(snapshot.CardEvents.Finished.ToString(), RamenDisplayColor.Yellow),
                    new("张, 剩余"),
                    new(snapshot.CardEvents.Remaining.ToString(), RamenDisplayColor.Yellow),
                    new("个"));
        }

        if (hasCurrentTrainingHistory && snapshot.TrainingFailures is { } failures)
        {
            yield return RamenDisplayLine.Styled(
                new("训练赌博: "),
                new(failures.GambleTimes.ToString(), RamenDisplayColor.Yellow),
                new("次, 失败"),
                new(failures.FailureTimes.ToString(), RamenDisplayColor.Yellow),
                new("次, 总失败率"),
                new(failures.TotalFailureRate.ToString(), RamenDisplayColor.Yellow),
                new("%"));
        }
    }

    static IEnumerable<RamenDisplayLine> ExtraRows(
        EventLoggerDisplaySnapshot snapshot,
        bool hasCurrentTrainingHistory)
    {
        if (snapshot.EventCount > 0)
            yield return RamenDisplayLine.Styled(
                new("事件数: "),
                new(snapshot.EventCount.ToString(), RamenDisplayColor.Yellow));

        if (snapshot.SuccessEvents.Appeared > 0)
        {
            yield return RamenDisplayLine.Styled(
                new("赌狗事件: "),
                new(snapshot.SuccessEvents.Appeared.ToString(), RamenDisplayColor.Yellow),
                new("次, 选择"),
                new(snapshot.SuccessEvents.Selected.ToString(), RamenDisplayColor.Yellow),
                new("次, 成功"),
                new(snapshot.SuccessEvents.Succeeded.ToString(), RamenDisplayColor.Yellow),
                new("次"));
        }

        if (hasCurrentTrainingHistory && snapshot.ScenarioFriend is { } friend)
        {
            yield return RamenDisplayLine.Styled(
                new($"{friend.Label}: 点击"),
                new(friend.ClickedTimes.ToString(), RamenDisplayColor.Aqua),
                new("次, 启动"),
                new(friend.ActivatedTimes.ToString(), RamenDisplayColor.Aqua),
                new("次"));
        }

        if (snapshot.InheritStats.Count > 0)
            yield return RamenDisplayLine.Styled(
                new("继承属性: "),
                new(string.Join('+', snapshot.InheritStats), RamenDisplayColor.Cyan));

        if (snapshot.RaceWinCount > 0)
            yield return RamenDisplayLine.Styled(
                new("胜场: "),
                new(snapshot.RaceWinCount.ToString(), RamenDisplayColor.Yellow));
    }

    static bool HasCurrentTrainingHistory(EventLoggerDisplaySnapshot snapshot, RamenTrainingDisplayContext context)
        => snapshot.CurrentTurn == context.Turn.Turn;
}
