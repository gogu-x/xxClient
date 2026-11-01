using System;
using System.Collections.Generic;

namespace XiuXian.Core
{
    /// <summary>
    /// 与 protobuf/Unity 解耦的战斗时间轴标记。每个 action 收齐
    /// start/skill/impact/end 后即可独立播放，无需等待整场 BattleFinished。
    /// </summary>
    public enum BattleTimelineMarkerKind
    {
        Other,
        ActionStarted,
        SkillUsed,
        Impact,
        ActionEnded,
    }

    public readonly struct BattleActionWindow
    {
        public BattleActionWindow(ulong action, string actorId, long startTick, long skillTick,
            long impactTick, long endTick)
        {
            Action = action;
            ActorId = actorId;
            StartTick = startTick;
            SkillTick = skillTick;
            ImpactTick = impactTick;
            EndTick = endTick;
        }

        public ulong Action { get; }
        public string ActorId { get; }
        public long StartTick { get; }
        public long SkillTick { get; }
        public long ImpactTick { get; }
        public long EndTick { get; }
    }

    /// <summary>
    /// 可按任意网络帧到达顺序收集动作 timing；单个 action 完整后立即可读，
    /// Seal 只表示服务端已结束输入并校验是否还有残缺动作。
    /// </summary>
    public sealed class BattleTimelinePlanner
    {
        private sealed class PendingAction
        {
            public string ActorId;
            public long StartTick = -1;
            public long SkillTick = -1;
            public long ImpactTick = -1;
            public long EndTick = -1;
            public bool HasSkill;
        }

        private readonly Dictionary<ulong, PendingAction> _pending = new();
        private readonly Dictionary<ulong, BattleActionWindow> _windows = new();
        private string _sealError;

        public bool IsSealed { get; private set; }

        public void Add(ulong action, long tick, BattleTimelineMarkerKind kind, string actorId)
        {
            if (IsSealed)
            {
                throw new InvalidOperationException("battle timeline is already sealed");
            }
            if (action == 0 || kind == BattleTimelineMarkerKind.Other)
            {
                return;
            }
            if (!_pending.TryGetValue(action, out var pending))
            {
                pending = new PendingAction();
                _pending.Add(action, pending);
            }

            if (!string.IsNullOrEmpty(actorId))
            {
                pending.ActorId ??= actorId;
            }
            switch (kind)
            {
                case BattleTimelineMarkerKind.ActionStarted:
                    pending.StartTick = SetConsistentTick(pending.StartTick, tick, action, "start");
                    break;
                case BattleTimelineMarkerKind.SkillUsed:
                    pending.HasSkill = true;
                    pending.SkillTick = SetConsistentTick(pending.SkillTick, tick, action, "skill");
                    break;
                case BattleTimelineMarkerKind.Impact:
                    // 多目标效果会在同一个 impact tick 产生多条事件。
                    pending.ImpactTick = SetConsistentTick(pending.ImpactTick, tick, action, "impact");
                    break;
                case BattleTimelineMarkerKind.ActionEnded:
                    pending.EndTick = SetConsistentTick(pending.EndTick, tick, action, "end");
                    break;
            }
            TryCompleteAction(action, pending);
        }

        public bool TrySeal(out string error)
        {
            if (IsSealed)
            {
                error = _sealError;
                return error == null;
            }
            IsSealed = true;

            foreach (var pair in _pending)
            {
                ulong action = pair.Key;
                var pending = pair.Value;
                // 跳过 ActionSkipped 等没有 SkillUsed、因此也没有 motion 的动作。
                if (!pending.HasSkill || _windows.ContainsKey(action))
                {
                    continue;
                }
                _sealError = $"action {action} has incomplete motion timing " +
                    $"(start={pending.StartTick}, skill={pending.SkillTick}, " +
                    $"impact={pending.ImpactTick}, end={pending.EndTick})";
                error = _sealError;
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// 用 SKILL_USED 事件自带的 start/impact/end 直接完成动作窗口，不必等待动作
        /// 结束时的 ActionEnded。流式下发时 ActionEnded 要晚整整一个动作才到，
        /// 只靠它会让攻击动画永远慢一拍。
        /// 窗口已存在时为空操作；后续真正到达的 Impact/ActionEnded 仍走一致性校验。
        /// </summary>
        public void SeedMotionWindow(ulong action, string actorId, long startTick, long impactTick, long endTick)
        {
            if (IsSealed || action == 0 || _windows.ContainsKey(action))
            {
                return;
            }
            if (startTick < 0 || startTick > impactTick || impactTick > endTick)
            {
                throw new InvalidOperationException(
                    $"action {action} has invalid seeded motion timing ({startTick}/{impactTick}/{endTick})");
            }

            if (!_pending.TryGetValue(action, out var pending))
            {
                pending = new PendingAction();
                _pending.Add(action, pending);
            }
            if (!string.IsNullOrEmpty(actorId))
            {
                pending.ActorId ??= actorId;
            }
            pending.StartTick = SetConsistentTick(pending.StartTick, startTick, action, "start");
            pending.ImpactTick = SetConsistentTick(pending.ImpactTick, impactTick, action, "impact");
            pending.EndTick = SetConsistentTick(pending.EndTick, endTick, action, "end");
            TryCompleteAction(action, pending);
        }

        public bool TryGetActionWindow(ulong action, out BattleActionWindow window) =>
            _windows.TryGetValue(action, out window);

        private void TryCompleteAction(ulong action, PendingAction pending)
        {
            if (_windows.ContainsKey(action) || !pending.HasSkill ||
                string.IsNullOrEmpty(pending.ActorId) || pending.StartTick < 0 ||
                pending.SkillTick < 0 || pending.ImpactTick < 0 || pending.EndTick < 0)
            {
                return;
            }
            if (pending.StartTick > pending.SkillTick || pending.SkillTick > pending.ImpactTick ||
                pending.ImpactTick > pending.EndTick)
            {
                throw new InvalidOperationException(
                    $"action {action} has invalid motion timing order " +
                    $"({pending.StartTick}/{pending.SkillTick}/{pending.ImpactTick}/{pending.EndTick})");
            }
            _windows.Add(action, new BattleActionWindow(action, pending.ActorId,
                pending.StartTick, pending.SkillTick, pending.ImpactTick, pending.EndTick));
        }

        private static long SetConsistentTick(long current, long incoming, ulong action, string field)
        {
            if (current >= 0 && current != incoming)
            {
                throw new InvalidOperationException(
                    $"action {action} has conflicting {field} ticks: {current} and {incoming}");
            }
            return incoming;
        }
    }

    /// <summary>
    /// 每个 key 最多一个 active owner。相同单位的动作严格 FIFO；不同单位可各自拥有 owner 并行执行。
    /// </summary>
    public sealed class UnitMotionQueue<T>
    {
        private sealed class Track
        {
            public readonly Queue<T> Items = new();
            public bool OwnerActive;
        }

        private readonly Dictionary<string, Track> _tracks = new();

        public int ActiveOwnerCount { get; private set; }

        /// <returns>仅当调用方应为该单位启动唯一 owner 时返回 true。</returns>
        public bool Enqueue(string unitId, T item)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                throw new ArgumentException("unit id is required", nameof(unitId));
            }
            if (!_tracks.TryGetValue(unitId, out var track))
            {
                track = new Track();
                _tracks.Add(unitId, track);
            }
            track.Items.Enqueue(item);
            if (track.OwnerActive)
            {
                return false;
            }
            track.OwnerActive = true;
            ActiveOwnerCount++;
            return true;
        }

        public bool TryTake(string unitId, out T item)
        {
            if (_tracks.TryGetValue(unitId, out var track) && track.OwnerActive && track.Items.Count > 0)
            {
                item = track.Items.Dequeue();
                return true;
            }
            item = default;
            return false;
        }

        public void CompleteOwner(string unitId)
        {
            if (!_tracks.TryGetValue(unitId, out var track) || !track.OwnerActive)
            {
                throw new InvalidOperationException($"unit {unitId} has no active motion owner");
            }
            if (track.Items.Count != 0)
            {
                throw new InvalidOperationException($"unit {unitId} still has queued motion actions");
            }
            track.OwnerActive = false;
            ActiveOwnerCount--;
            _tracks.Remove(unitId);
        }

        public void Clear()
        {
            _tracks.Clear();
            ActiveOwnerCount = 0;
        }
    }
}
