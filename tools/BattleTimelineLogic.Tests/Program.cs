using System;
using XiuXian.Core;

internal static class Program
{
    private static int Main()
    {
        try
        {
            CompletedActionIsPlayableBeforeBattleFinished();
            IncompleteTimingIsRejectedAtBattleFinished();
            SameUnitHasOneOwnerAtTouchingBoundaryWhileUnitsRunInParallel();
            SeededWindowIsPlayableFromSkillUsedAlone();
            SeededWindowWorksWhenSeedArrivesBeforeMarker();
            SeededWindowRejectsInvalidTiming();
            Console.WriteLine("BattleTimelineLogic tests passed (6/6).");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void CompletedActionIsPlayableBeforeBattleFinished()
    {
        var planner = new BattleTimelinePlanner();
        // 模拟 SkillUsed 先到，而 start/impact/end 分散在后续网络帧。
        planner.Add(7, 20, BattleTimelineMarkerKind.SkillUsed, "actor-a");
        Assert(!planner.TryGetActionWindow(7, out _), "partial action must not be playable");
        planner.Add(7, 25, BattleTimelineMarkerKind.ActionEnded, "actor-a");
        planner.Add(7, 22, BattleTimelineMarkerKind.Impact, "actor-a");
        Assert(!planner.TryGetActionWindow(7, out _), "action without start must remain pending");
        planner.Add(7, 20, BattleTimelineMarkerKind.ActionStarted, "actor-a");
        Assert(planner.TryGetActionWindow(7, out var window),
            "complete action must become playable before BattleFinished seals the stream");
        Assert(!planner.IsSealed, "publishing one action must not seal the battle stream");
        Assert(window.StartTick == 20 && window.ImpactTick == 22 && window.EndTick == 25,
            "streamed action timing is inaccurate");
        Assert(planner.TrySeal(out var error), error);
    }

    private static void IncompleteTimingIsRejectedAtBattleFinished()
    {
        var planner = new BattleTimelinePlanner();
        planner.Add(8, 20, BattleTimelineMarkerKind.ActionStarted, "actor-a");
        planner.Add(8, 20, BattleTimelineMarkerKind.SkillUsed, "actor-a");
        planner.Add(8, 22, BattleTimelineMarkerKind.Impact, "actor-a");
        planner.Add(8, 24, BattleTimelineMarkerKind.ActionEnded, "actor-a");
        Assert(planner.TryGetActionWindow(8, out _),
            "completed action must stream even if a later action is incomplete");

        planner.Add(9, 30, BattleTimelineMarkerKind.ActionStarted, "actor-b");
        planner.Add(9, 30, BattleTimelineMarkerKind.SkillUsed, "actor-b");
        planner.Add(9, 34, BattleTimelineMarkerKind.ActionEnded, "actor-b");
        Assert(!planner.TrySeal(out var error), "missing impact must reject the final stream seal");
        Assert(error != null && error.Contains("incomplete motion timing"), "missing timing error absent");
        Assert(!planner.TrySeal(out _), "failed seal must remain failed");
        Assert(planner.TryGetActionWindow(8, out _),
            "final validation must not revoke an action that already streamed");
        Assert(!planner.TryGetActionWindow(9, out _), "incomplete action must not become playable");
    }

    private static void SameUnitHasOneOwnerAtTouchingBoundaryWhileUnitsRunInParallel()
    {
        var queue = new UnitMotionQueue<BattleActionWindow>();
        var first = new BattleActionWindow(1, "actor-a", 1, 1, 3, 5);
        var touching = new BattleActionWindow(2, "actor-a", 5, 5, 6, 8);
        var otherUnit = new BattleActionWindow(3, "actor-b", 5, 5, 7, 9);

        Assert(queue.Enqueue("actor-a", first), "first actor-a action must start its owner");
        Assert(!queue.Enqueue("actor-a", touching),
            "touching actor-a action must reuse the existing owner");
        Assert(queue.Enqueue("actor-b", otherUnit), "actor-b must get an independent parallel owner");
        Assert(queue.ActiveOwnerCount == 2, "expected one owner per unit");

        Assert(queue.TryTake("actor-a", out var actualFirst) && actualFirst.Action == 1,
            "actor-a FIFO first action mismatch");
        Assert(queue.TryTake("actor-a", out var actualSecond) && actualSecond.Action == 2,
            "actor-a touching action was not serialized");
        queue.CompleteOwner("actor-a");
        Assert(queue.TryTake("actor-b", out var actualOther) && actualOther.Action == 3,
            "actor-b parallel track mismatch");
        queue.CompleteOwner("actor-b");
        Assert(queue.ActiveOwnerCount == 0, "motion owners leaked");
    }

    // SKILL_USED 自带完整动作窗口时，客户端收到单条事件就应该能起播，
    // 不必等待动作结束的 ActionEnded（流式下发时它会晚整整一个动作）。
    private static void SeededWindowIsPlayableFromSkillUsedAlone()
    {
        var planner = new BattleTimelinePlanner();
        planner.Add(7, 20, BattleTimelineMarkerKind.SkillUsed, "actor-a");
        planner.SeedMotionWindow(7, "actor-a", 20, 23, 25);
        Assert(planner.TryGetActionWindow(7, out var window),
            "seeded SKILL_USED must be playable without ActionEnded");
        Assert(window.StartTick == 20 && window.SkillTick == 20 &&
               window.ImpactTick == 23 && window.EndTick == 25,
            "seeded window timing is inaccurate");

        // 随后真正到达的 Impact / ActionEnded 必须与已落窗口一致，不得抛异常。
        planner.Add(7, 23, BattleTimelineMarkerKind.Impact, "actor-a");
        planner.Add(7, 25, BattleTimelineMarkerKind.ActionEnded, "actor-a");
        Assert(planner.TryGetActionWindow(7, out var after),
            "window must survive the confirming events");
        Assert(after.ImpactTick == 23 && after.EndTick == 25,
            "confirming events altered the seeded window");

        // 重复落窗必须幂等（重投或重复下发场景）。
        planner.SeedMotionWindow(7, "actor-a", 20, 23, 25);
        Assert(planner.TrySeal(out var error), error);
    }

    // 事件到达顺序不应影响结果：先落窗后收到 SKILL_USED 也要能成窗。
    private static void SeededWindowWorksWhenSeedArrivesBeforeMarker()
    {
        var planner = new BattleTimelinePlanner();
        planner.SeedMotionWindow(9, "actor-b", 30, 33, 35);
        Assert(!planner.TryGetActionWindow(9, out _),
            "seed without SKILL_USED must not become playable");
        planner.Add(9, 30, BattleTimelineMarkerKind.SkillUsed, "actor-b");
        Assert(planner.TryGetActionWindow(9, out var window),
            "marker arriving after the seed must complete the window");
        Assert(window.ImpactTick == 33 && window.EndTick == 35, "seed timing was lost");
    }

    // 服务端若下发了非法窗口，必须显式失败而不是静默播错动作。
    private static void SeededWindowRejectsInvalidTiming()
    {
        var planner = new BattleTimelinePlanner();
        var threw = false;
        try
        {
            planner.SeedMotionWindow(11, "actor-c", 30, 20, 25);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }
        Assert(threw, "impact before start must be rejected");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
