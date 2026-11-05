// 仅供独立规则校验，不进入 Unity Assets 编译，也不读取或修改玩家存档。
using System;
using XiuXian.Core;

internal static class CultivationProgressCheck
{
    private static int checks;
    private const long Origin = 1800000000000L;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        checks++;
    }
    private static void Near(double actual, double expected, string name)
    {
        Check(Math.Abs(actual - expected) < .000001, name);
    }
    public static int Main()
    {
        string message;
        var initial = new CultivationLocalState(null, Origin, 0);
        var snapshot = initial.Snapshot();
        Check(snapshot.Rank == 1 && snapshot.RequiredProgress == 600, "Initial rank and requirement");
        Check(snapshot.Pills[0].Count == 3 && snapshot.Pills[1].Count == 2 && snapshot.Pills[2].Count == 1, "Starter pills");
        initial.Advance(Origin + 60000, 60);
        Near(initial.Snapshot().Progress, 10, "Natural one minute");
        Check(!initial.TryBreakthrough(60, out message), "Cannot break early");
        Check(!initial.TryUsePill(0, 60, out message), "Invalid pill rejected");
        Check(initial.TryUsePill(1, 60, out message), "Pill succeeds");
        Near(initial.Snapshot().Progress, 130, "Pill gain");
        Check(!initial.TryUsePill(1, 60.1, out message), "Double click rejected");
        Check(initial.Snapshot().Pills[0].Count == 2, "Double click consumed once");
        Check(initial.TryUsePill(3, 61, out message), "Fill with pill");
        Near(initial.Snapshot().Progress, 600, "Gain clamps to requirement");
        Check(initial.Snapshot().CanBreakthrough && initial.Snapshot().Rank == 1, "Full does not automatically break");
        Check(!initial.TryUsePill(2, 62, out message) && initial.Snapshot().Pills[1].Count == 2, "Full inventory protected");
        Check(initial.TryBreakthrough(62, out message), "Breakthrough succeeds");
        Check(initial.Snapshot().Rank == 2 && initial.Snapshot().Progress == 0, "Breakthrough clears progress");
        Check(!initial.TryBreakthrough(62, out message) && initial.Snapshot().Rank == 2, "Duplicate breakthrough rejected");
        Check(!initial.TryUsePill(3, 63, out message), "Empty inventory rejected");
        snapshot = initial.Snapshot();
        snapshot.Pills[1] = null;
        Check(initial.Snapshot().Pills[1].Count == 2, "Snapshot cannot alter inventory");

        var offline = new CultivationLocalState(new CultivationLocalSave { rank = 15, lastUtcMilliseconds = Origin }, Origin + 24L * 3600000, 0);
        Near(offline.Snapshot().Progress, 38 * 480, "Offline capped at eight hours");
        var rollback = new CultivationLocalState(new CultivationLocalSave { lastUtcMilliseconds = Origin }, Origin - 3600000, 0);
        Near(rollback.Snapshot().Progress, 0, "Backwards wall clock has no reward");
        rollback.Advance(Origin + 365L * 86400000, 60);
        Near(rollback.Snapshot().Progress, 10, "Online wall jump ignores fabricated year");
        rollback.Advance(Origin + 365L * 86400000, 50);
        Near(rollback.Snapshot().Progress, 10, "Backwards monotonic input ignored");

        var final = new CultivationLocalState(new CultivationLocalSave { rank = 19, progress = 600L * 19 * 19, lastUtcMilliseconds = Origin }, Origin, 0);
        Check(final.TryBreakthrough(1, out message), "Final breakthrough");
        var end = final.Snapshot();
        Check(end.IsMaxRank && end.Progress01 == 1 && end.RequiredProgress == 0 && end.GainPerMinute == 0 && !end.CanBreakthrough, "Maximum rank state");
        Check(!final.TryUsePill(1, 2, out message) && !final.TryBreakthrough(2, out message), "Maximum rank blocks mutations");
        final.Advance(Origin + 86400000, 86400);
        Near(final.Snapshot().Progress, 0, "Maximum rank stops growth");
        var bad = new CultivationLocalState(new CultivationLocalSave { rank = -10, progress = double.NaN, pills = new[] { -2 }, lastUtcMilliseconds = Origin }, Origin, 0);
        Check(bad.Snapshot().Rank == 1 && bad.Snapshot().Progress == 0 && bad.Snapshot().Pills[0].Count == 0 && bad.Snapshot().Pills[2].Count == 0, "Damaged fields safely clamped without regranting pills");
        Check(CultivationProgress.BuildStorageKey("account-a", 1) != CultivationProgress.BuildStorageKey("account-b", 1), "Accounts isolated");
        Check(CultivationProgress.BuildStorageKey("account-a", 1) != CultivationProgress.BuildStorageKey("account-a", 2), "Servers isolated");
        Console.WriteLine("CultivationProgress: " + checks + " checks passed; no player data accessed.");
        return 0;
    }
}

// 独立编译时仅满足外层 Unity 适配层依赖；校验使用真实的 CultivationLocalState。
namespace UnityEngine
{
    internal enum RuntimeInitializeLoadType { SubsystemRegistration }
    [AttributeUsage(AttributeTargets.Method)] internal sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType value) { } }
    internal static class Application { public static event Action quitting; }
    internal static class PlayerPrefs
    {
        public static bool HasKey(string key) { throw new NotSupportedException(); }
        public static string GetString(string key, string fallback = "") { throw new NotSupportedException(); }
        public static void SetString(string key, string value) { throw new NotSupportedException(); }
        public static void Save() { throw new NotSupportedException(); }
    }
    internal static class JsonUtility
    {
        public static T FromJson<T>(string value) { throw new NotSupportedException(); }
        public static string ToJson(object value) { throw new NotSupportedException(); }
    }
    internal static class Debug { public static void LogWarning(object message) { throw new NotSupportedException(); } }
}
namespace XiuXian.Core
{
    internal static class GuestAccount { public static string GetOrCreateAccount() { throw new NotSupportedException(); } }
    internal class UIManager { public static UIManager Instance; public ServerInfo SelectedServer; }
    internal class ServerInfo { public int ServerId; }
}
