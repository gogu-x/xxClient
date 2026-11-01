using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XiuXian.Core
{
    public sealed partial class BattleView
    {
        private List<UnitVisual> CollectActionTargets(global::BattleEvent skillEvent)
        {
            var targets = new List<UnitVisual>();
            var ids = new HashSet<string>(System.StringComparer.Ordinal);
            void AddTarget(string id)
            {
                if (string.IsNullOrEmpty(id) || !ids.Add(id) ||
                    IsTargetActionAborted(skillEvent.Action, skillEvent.ActorId, id)) return;
                var unit = EnsureUnitVisual(id);
                if (unit != null) targets.Add(unit);
            }
            if (skillEvent.TargetIds.Count > 0)
                foreach (string id in skillEvent.TargetIds) AddTarget(id);
            else
                AddTarget(skillEvent.TargetId);
            return targets;
        }

        private IEnumerator AnimateSkillProjectiles(UnitVisual attacker, List<UnitVisual> targets,
            float travelDuration, float fadeDuration, ulong action, bool stunSkill)
        {
            // 同一次施法的弹道逐帧并行推进，共用命中时刻；不串行攻击，也不重播角色出手。
            var projectiles = new List<IEnumerator>(targets.Count);
            foreach (var target in targets)
                if (!IsTargetActionAborted(action, attacker.UnitId, target.UnitId))
                    projectiles.Add(AnimateBlueBeam(attacker, target, travelDuration, fadeDuration, action, stunSkill));
            if (targets.Count > 1)
                projectiles.Add(AnimateAreaImpact(attacker, targets, travelDuration, fadeDuration, action));
            try
            {
                while (projectiles.Count > 0)
                {
                    for (int i = projectiles.Count - 1; i >= 0; i--)
                    {
                        // AnimateBlueBeam 仅逐帧 yield null，弹道和命中淡出都由本协程统一调度。
                        if (projectiles[i].MoveNext()) continue;
                        (projectiles[i] as System.IDisposable)?.Dispose();
                        projectiles.RemoveAt(i);
                    }
                    if (projectiles.Count > 0) yield return null;
                }
            }
            finally
            {
                foreach (var projectile in projectiles)
                    (projectile as System.IDisposable)?.Dispose();
            }
        }

        private IEnumerator AnimateAreaImpact(UnitVisual attacker, List<UnitVisual> targets,
            float travelDuration, float fadeDuration, ulong action)
        {
            float elapsed = 0f;
            while (elapsed < travelDuration)
            {
                if (IsActionAborted(action, attacker.UnitId)) yield break;
                elapsed += PresentationDelta;
                yield return null;
            }
            if (fadeDuration <= 0f || IsActionAborted(action, attacker.UnitId)) yield break;
            Vector3 center = Vector3.zero;
            int count = 0;
            foreach (var target in targets)
            {
                if (target.Sphere == null || _cancelledActionTargets.Contains((action, target.UnitId))) continue;
                center += GetBeamTarget(target);
                count++;
            }
            if (count < 2) yield break;
            center /= count;
            float radius = .42f;
            foreach (var target in targets)
                if (target.Sphere != null && !_cancelledActionTargets.Contains((action, target.UnitId)))
                    radius = Mathf.Max(radius, Vector2.Distance(center, GetBeamTarget(target)) + .32f);

            var effect = new GameObject("AreaImpact-" + action);
            effect.transform.SetParent(_worldRoot.transform, false);
            _transientEffects.Add(effect);
            try
            {
                var ring = CreateFxRenderer(effect.transform, "AreaInkRing", 0, 1, 1f, 74);
                if (ring == null) yield break;
                ring.transform.position = center;
                elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += PresentationDelta;
                    float t = Mathf.Clamp01(elapsed / fadeDuration);
                    float diameter = radius * 2f * Mathf.Lerp(.84f, 1.08f, t);
                    ring.transform.localScale = new Vector3(diameter, diameter * .45f, 1f);
                    ring.color = new Color(1, 1, 1, .52f * (1f - t));
                    yield return null;
                }
            }
            finally { DestroyTransientEffect(effect); }
        }
    }
}
