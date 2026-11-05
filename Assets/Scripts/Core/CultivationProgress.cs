using System;
using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace XiuXian.Core
{
    /// <summary>修炼的本机体验存档；不代表服务器角色属性，也不修改旧天赋存档。</summary>
    public static class CultivationProgress
    {
        private const string SavePrefix = "XiuXian.Cultivation.Local.v1.";
        private const double SaveIntervalSeconds = 30;
        private static CultivationLocalState state;
        private static string currentKey;
        private static double lastSaveTime;

        /// <summary>服丹或突破成功后通知；自然进度由页面定时读取，避免递归刷新。</summary>
        public static event Action Changed;

        /// <summary>读取当前账号在当前区服的本机修炼状态，并结算自然修为。</summary>
        public static CultivationSnapshot GetSnapshot()
        {
            var current = GetState();
            current.Advance(UtcMilliseconds(), MonotonicSeconds());
            if (MonotonicSeconds() - lastSaveTime >= SaveIntervalSeconds) SaveCurrent();
            return current.Snapshot();
        }

        /// <summary>服用一枚指定丹药；编号为 1、2、3，失败时不扣库存。</summary>
        public static bool TryUsePill(int id, out string message)
        {
            var current = GetState();
            var now = MonotonicSeconds();
            current.Advance(UtcMilliseconds(), now);
            if (!current.TryUsePill(id, now, out message)) return false;
            SaveCurrent();
            Changed?.Invoke();
            return true;
        }

        /// <summary>修为圆满时进入下一境界，当前修为归零。</summary>
        public static bool TryBreakthrough(out string message)
        {
            var current = GetState();
            var now = MonotonicSeconds();
            current.Advance(UtcMilliseconds(), now);
            if (!current.TryBreakthrough(now, out message)) return false;
            SaveCurrent();
            Changed?.Invoke();
            return true;
        }

        /// <summary>页面关闭或应用退出时保存；不为尚未打开的页面创建新存档。</summary>
        public static void Save()
        {
            if (state == null) return;
            state.Advance(UtcMilliseconds(), MonotonicSeconds());
            SaveCurrent();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            state = null;
            currentKey = null;
            lastSaveTime = 0;
            Changed = null;
            Application.quitting -= Save;
            Application.quitting += Save;
        }

        private static CultivationLocalState GetState()
        {
            string key = ResolveStorageKey();
            if (state != null && key == currentKey) return state;
            Save();
            currentKey = key;
            CultivationLocalSave data = null;
            if (PlayerPrefs.HasKey(key))
            {
                try { data = JsonUtility.FromJson<CultivationLocalSave>(PlayerPrefs.GetString(key)); }
                catch (Exception error) { UnityEngine.Debug.LogWarning("[CultivationProgress] 本机修炼存档无法读取：" + error.Message); }
            }
            state = new CultivationLocalState(data, UtcMilliseconds(), MonotonicSeconds());
            SaveCurrent();
            return state;
        }

        private static string ResolveStorageKey()
        {
            string account = string.Empty;
#if UNITY_EDITOR || XIUXIAN_DEV_TOOLS
            account = PlayerPrefs.GetString("XiuXian.DeveloperAccount", string.Empty).Trim();
#endif
            if (string.IsNullOrEmpty(account)) account = GuestAccount.GetOrCreateAccount();
            var manager = UIManager.Instance;
            var server = manager != null ? manager.SelectedServer : null;
            int serverId = server != null ? server.ServerId : 0;
            // 当前登录契约为每账号、每区服一个角色。UID 在首次注册响应后尚未写入
            // SelectedServer，故使用稳定的账号+区服，避免同一角色下次登录得到新存档。
            return BuildStorageKey(account, serverId);
        }

        internal static string BuildStorageKey(string account, int serverId)
        {
            return SavePrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(account ?? string.Empty)) + "." + serverId;
        }

        private static void SaveCurrent()
        {
            if (state == null || string.IsNullOrEmpty(currentKey)) return;
            PlayerPrefs.SetString(currentKey, JsonUtility.ToJson(state.Data));
            PlayerPrefs.Save();
            lastSaveTime = MonotonicSeconds();
        }

        private static long UtcMilliseconds() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        private static double MonotonicSeconds() => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
    }

    /// <summary>供界面读取的修炼快照；修改返回的丹药数组不会改变存档。</summary>
    public sealed class CultivationSnapshot
    {
        public int Rank { get; internal set; }
        public string RealmName { get; internal set; }
        public string NextRealmName { get; internal set; }
        public double Progress { get; internal set; }
        public long RequiredProgress { get; internal set; }
        public float Progress01 { get; internal set; }
        public double GainPerMinute { get; internal set; }
        public bool CanBreakthrough { get; internal set; }
        public bool IsMaxRank { get; internal set; }
        public CultivationPillSnapshot[] Pills { get; internal set; }
    }

    /// <summary>一类丹药的本机库存与修为收益。</summary>
    public sealed class CultivationPillSnapshot
    {
        public int Id { get; internal set; }
        public string Name { get; internal set; }
        public int Count { get; internal set; }
        public int Gain { get; internal set; }
        public string Description { get; internal set; }
    }

    [Serializable]
    internal sealed class CultivationLocalSave
    {
        public int version = 1;
        public int rank = 1;
        public double progress;
        public int[] pills = { 3, 2, 1 };
        public long lastUtcMilliseconds;
    }

    /// <summary>不依赖场景或存储的规则计算，时间由调用方提供以便验证边界。</summary>
    internal sealed class CultivationLocalState
    {
        internal const double MaxOfflineSeconds = 8 * 60 * 60;
        private const double ActionCooldownSeconds = .35;
        private static readonly string[] Realms =
        {
            "炼气初期", "炼气中期", "炼气后期", "炼气圆满", "筑基初期",
            "筑基中期", "筑基后期", "金丹初期", "金丹中期", "金丹后期",
            "元婴初期", "元婴中期", "元婴后期", "化神初期", "化神中期",
            "化神后期", "合体初期", "合体后期", "渡劫期", "大乘期"
        };
        private static readonly string[] PillNames = { "聚灵丹", "凝元丹", "太清丹" };
        private static readonly int[] PillGains = { 120, 300, 600 };
        internal CultivationLocalSave Data { get; }
        private double lastMonotonicSeconds;
        private double nextActionSeconds;
        private long Required => IsMaxRank ? 0 : 600L * Data.rank * Data.rank;
        private double PerMinute => IsMaxRank ? 0 : 10 + (Data.rank - 1) * 2;
        private bool IsMaxRank => Data.rank >= Realms.Length;

        internal CultivationLocalState(CultivationLocalSave saved, long utcMilliseconds, double monotonicSeconds)
        {
            Data = saved ?? new CultivationLocalSave { lastUtcMilliseconds = utcMilliseconds };
            Sanitize();
            if (Data.lastUtcMilliseconds > 0 && utcMilliseconds > Data.lastUtcMilliseconds)
                Gain(Math.Min(MaxOfflineSeconds, (utcMilliseconds - Data.lastUtcMilliseconds) / 1000d));
            Data.lastUtcMilliseconds = Math.Max(Data.lastUtcMilliseconds, utcMilliseconds);
            lastMonotonicSeconds = monotonicSeconds;
            nextActionSeconds = double.NegativeInfinity;
        }

        internal void Advance(long utcMilliseconds, double monotonicSeconds)
        {
            // 运行中只相信单调时间。调整系统日期不会瞬间增加修为；暂停过久同样按八小时封顶。
            if (!double.IsNaN(monotonicSeconds) && !double.IsInfinity(monotonicSeconds) && monotonicSeconds > lastMonotonicSeconds)
            {
                Gain(Math.Min(MaxOfflineSeconds, monotonicSeconds - lastMonotonicSeconds));
                lastMonotonicSeconds = monotonicSeconds;
            }
            // 倒退的系统时钟不能重新获得已经结算过的离线时间。
            Data.lastUtcMilliseconds = Math.Max(Data.lastUtcMilliseconds, utcMilliseconds);
        }

        internal bool TryUsePill(int id, double monotonicSeconds, out string message)
        {
            if (id < 1 || id > PillNames.Length) { message = "请选择有效丹药"; return false; }
            if (IsMaxRank) { message = "已臻大乘，暂无更高境界"; return false; }
            if (Data.progress >= Required) { message = "修为已满，请先突破"; return false; }
            if (Data.pills[id - 1] <= 0) { message = PillNames[id - 1] + "已用完"; return false; }
            if (!CanAct(monotonicSeconds, out message)) return false;
            double gain = Math.Min(PillGains[id - 1], Required - Data.progress);
            Data.pills[id - 1]--;
            Data.progress += gain;
            nextActionSeconds = monotonicSeconds + ActionCooldownSeconds;
            message = PillNames[id - 1] + "已炼化，修为 +" + gain.ToString("0.#");
            return true;
        }

        internal bool TryBreakthrough(double monotonicSeconds, out string message)
        {
            if (IsMaxRank) { message = "已臻大乘，暂无更高境界"; return false; }
            if (Data.progress < Required) { message = "修为尚未圆满"; return false; }
            if (!CanAct(monotonicSeconds, out message)) return false;
            Data.rank++;
            Data.progress = 0;
            nextActionSeconds = monotonicSeconds + ActionCooldownSeconds;
            message = "突破成功 · " + Realms[Data.rank - 1];
            return true;
        }

        internal CultivationSnapshot Snapshot()
        {
            var pills = new CultivationPillSnapshot[PillNames.Length];
            for (int i = 0; i < pills.Length; i++)
                pills[i] = new CultivationPillSnapshot
                {
                    Id = i + 1, Name = PillNames[i], Count = Data.pills[i], Gain = PillGains[i],
                    Description = "服用增加 " + PillGains[i] + " 修为"
                };
            return new CultivationSnapshot
            {
                Rank = Data.rank, RealmName = Realms[Data.rank - 1],
                NextRealmName = IsMaxRank ? "已臻化境" : Realms[Data.rank],
                Progress = Data.progress, RequiredProgress = Required,
                Progress01 = IsMaxRank ? 1 : (float)(Data.progress / Required),
                GainPerMinute = PerMinute, IsMaxRank = IsMaxRank,
                CanBreakthrough = !IsMaxRank && Data.progress >= Required, Pills = pills
            };
        }

        private bool CanAct(double now, out string message)
        {
            if (now < nextActionSeconds || double.IsNaN(now) || double.IsInfinity(now))
            { message = "丹力正在炼化，请稍候"; return false; }
            message = string.Empty;
            return true;
        }

        private void Gain(double seconds)
        {
            if (!IsMaxRank && seconds > 0) Data.progress = Math.Min(Required, Data.progress + PerMinute * seconds / 60d);
        }

        private void Sanitize()
        {
            Data.rank = Math.Max(1, Math.Min(Realms.Length, Data.rank));
            if (double.IsNaN(Data.progress) || double.IsInfinity(Data.progress)) Data.progress = 0;
            Data.progress = IsMaxRank ? 0 : Math.Max(0, Math.Min(Required, Data.progress));
            var oldPills = Data.pills;
            Data.pills = new int[PillNames.Length];
            if (oldPills != null)
                for (int i = 0; i < Math.Min(oldPills.Length, Data.pills.Length); i++)
                    Data.pills[i] = Math.Max(0, Math.Min(999999, oldPills[i]));
            Data.lastUtcMilliseconds = Math.Max(0, Data.lastUtcMilliseconds);
            Data.version = 1;
        }
    }
}
