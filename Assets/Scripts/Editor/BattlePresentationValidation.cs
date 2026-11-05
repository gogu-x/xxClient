using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using XiuXian.Core;
using XiuXian.Network;

namespace XiuXian.EditorTools
{
    /// <summary>在隔离视图中验证战斗表现；不创建战斗请求，不读写玩家数据。</summary>
    public static class BattlePresentationValidation
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo InstanceField = typeof(BattleView).GetField("_instance",
            BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly string[] BattleMessages =
            { "StartBattleAck", "BattleCreatedNtf", "BattleActionNtf", "BattleFinishedNtf" };

        public static string Run()
        {
            Check(Application.isPlaying, "Enter Play Mode before validating battle presentation.");
            var previous = (BattleView)InstanceField.GetValue(null);
            RequireIdle(previous);
            var network = UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
            Check(network != null, "Run in the initialized Bootstrap scene; validation will not create a NetworkManager.");
            var saved = new SavedView(previous, network);
            GameObject root = null;
            BattleView view = null;
            try
            {
                saved.Suspend();
                root = new GameObject("BattlePresentationValidation", typeof(RectTransform), typeof(Canvas));
                root.hideFlags = HideFlags.HideAndDontSave;
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                root.GetComponent<Canvas>().enabled = false;
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(Screen.width, Screen.height);
                var page = new GameObject("IsolatedBattleView", typeof(RectTransform));
                page.transform.SetParent(root.transform, false);
                var rect = page.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                view = page.AddComponent<BattleView>();
                // Awake 构造真实页面。OnEnable 短暂注册后立即解绑，全程不执行 Show/BeginBattle。
                Invoke(view, "UnregisterNetworkHandlers");
                var world = Read<GameObject>(view, "_worldRoot");
                world.hideFlags = HideFlags.HideAndDontSave;
                foreach (var camera in world.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                foreach (var light in world.GetComponentsInChildren<Light>(true)) light.enabled = false;
                Invoke(view, "UpdateBattleDesignLayout");

                const string actor = "validation-actor";
                const string first = "monster-validation-first";
                const string second = "monster-validation-second";
                const string dead = "monster-validation-dead";
                Seed(view, actor, false, 671, 1208);
                Seed(view, first, true, 3, 1208);
                Seed(view, second, true, 200, 250);
                Seed(view, dead, true, 0, 50);

                VerifyInkLayers(view, actor);
                VerifyControlState(view, actor);
                VerifyLowHealth(view, first);
                VerifyTargets(view, actor, first, second, dead);
                VerifyTargetCancellation(view, actor, first, second);
                VerifyLethalDamage(view, actor, second);
                return "PASS: camera-facing ink sprites use independent control/hit/cast layers with no 3D model or Animator; flight stays within its own formation slot; prebuilt stun stars start hidden, reuse instances and hide on recovery; stun transitions preserve the scheduled release/end times of an in-flight cast; 3/1208 HP retains at least 2 screen pixels with exact HP text; target_dead preserves actor HP 671 and remaining AOE targets; lethal damage stops casting immediately; single/AOE targets deduplicate and exclude dead units. Isolated objects and original view/network handlers restored.";
            }
            finally
            {
                try
                {
                    if (view == null && root != null) view = root.GetComponentInChildren<BattleView>(true);
                    if (view != null)
                    {
                        view.gameObject.SetActive(false);
                        Invoke(view, "UnregisterNetworkHandlers");
                        Invoke(view, "ClearUnitVisuals");
                        var world = Read<GameObject>(view, "_worldRoot");
                        if (world != null) UnityEngine.Object.DestroyImmediate(world);
                        Write(view, "_worldRoot", null);
                    }
                }
                finally
                {
                    try
                    {
                        if (root != null) UnityEngine.Object.DestroyImmediate(root);
                    }
                    finally
                    {
                        saved.Restore();
                    }
                }
            }
        }

        private static void RequireIdle(BattleView view)
        {
            if (view == null) return;
            Check(Read<object>(view, "_playRoutine") == null &&
                Read<object>(view, "_battleCreateTimeout") == null &&
                Read<object>(view, "_pendingFinished") == null &&
                Read<int>(view, "_runningAnimations") == 0,
                "Wait for battle playback and result settlement before running isolated validation.");
            Check(string.IsNullOrEmpty(Read<string>(view, "_battleId")) ||
                Read<GameObject>(view, "_resultPage") != null || Read<bool>(view, "_canBeginBattle"),
                "An unfinished live battle cannot be suspended safely for this check.");
            foreach (var unit in Read<IDictionary>(view, "_unitVisuals").Values)
                Check(!Read<bool>(unit, "AttackPlaying") && Read<object>(unit, "DeathRoutine") == null,
                    "Wait for attack/death animation recovery before running validation.");
        }

        private static object Seed(BattleView view, string id, bool enemy, long hp, long maxHp)
        {
            var unit = Invoke(view, "EnsureUnitVisual", id, (bool?)enemy);
            Invoke(view, "UpdateHealth", id, hp, maxHp);
            return unit;
        }

        private static void VerifyInkLayers(BattleView view, string actor)
        {
            var camera = Read<Camera>(view, "_battleCamera");
            foreach (var unit in Read<IDictionary>(view, "_unitVisuals").Values)
            {
                var body = Read<Transform>(unit, "Body");
                var sprite = Read<SpriteRenderer>(unit, "Illustration");
                var motion = Read<Transform>(unit, "Sphere");
                var cast = Read<Transform>(unit, "CastRoot");
                var hit = Read<Transform>(unit, "ReactionRoot");
                var control = Read<Transform>(unit, "ControlRoot");
                Check(sprite != null && sprite.sprite != null && sprite.transform == body,
                    "Every actual battle unit must have a visible imported ink foreground.");
                Check(body.parent == cast && cast.parent == hit && hit.parent == control && control.parent == motion,
                    "Flight, control, hit reaction and casting must have independent transform ownership.");
                Check(motion.GetComponentsInChildren<Animator>(true).Length == 0 &&
                    motion.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0,
                    "The ink battle must not instantiate the old 3D model or Animator.");
                Check(Vector3.Dot(body.forward, camera.transform.forward) > .999f,
                    "Ink sprites must face the battle camera; no Y-axis attack facing is allowed.");
            }
            var actorUnit = Unit(view, actor);
            float time = Read<float>(view, "_presentationTime");
            Vector3 home = Read<Vector3>(actorUnit, "Home");
            try
            {
                for (int i = 0; i < 12; i++)
                {
                    Write(view, "_presentationTime", time + i * .25f);
                    var position = (Vector3)Invoke(view, "AirCastPosition", actorUnit);
                    Check(Vector3.Distance(home, position) <= .33f,
                        "A ranged cast may drift inside its own formation slot, not travel to an enemy.");
                }
            }
            finally { Write(view, "_presentationTime", time); }
        }

        private static void VerifyControlState(BattleView view, string actor)
        {
            foreach (var created in Read<IDictionary>(view, "_unitVisuals").Values)
            {
                Check(!Read<bool>(created, "IsStunned"), "New units must not start stunned.");
                var prepared = Read<SpriteRenderer[]>(created, "StunStars");
                Check(prepared != null && prepared.Length > 0,
                    "Stun stars must be prepared when the unit is created, before its first control event.");
                RequireControlStars(created, prepared, false);
            }

            var unit = Unit(view, actor);
            var stars = (SpriteRenderer[])Read<SpriteRenderer[]>(unit, "StunStars").Clone();
            float time = Read<float>(view, "_presentationTime");
            try
            {
                Invoke(view, "PlayCharacterAttack", unit, .1f, time + 2f);
                RequireScheduledAttack(unit, time, time + .1f, time + 2f);
                // 使用真实跳过事件进入控制状态，不伪造缺少 motion window 的 SkillUsed。
                Invoke(view, "DispatchEvent", new BattleEvent
                {
                    Type = BattleEventType.ActionSkipped, Action = 790, ActorId = actor, Reason = BattleEventReason.Stunned,
                });
                Invoke(view, "UpdateUnitControlVisuals");
                Invoke(view, "UpdateCharacterAnimations");
                Invoke(view, "UpdateUnitControlStars");
                Check(Read<bool>(unit, "IsStunned"), "A stun ActionSkipped event must enter the control state.");
                RequireControlStars(unit, stars, true);
                RequireScheduledAttack(unit, time, time + .1f, time + 2f);

                Invoke(view, "DispatchEvent", new BattleEvent
                {
                    Type = BattleEventType.ActionSkipped, Action = 791, ActorId = actor, Reason = BattleEventReason.Unspecified,
                });
                Invoke(view, "UpdateUnitControlVisuals");
                Invoke(view, "UpdateCharacterAnimations");
                Invoke(view, "UpdateUnitControlStars");
                Check(!Read<bool>(unit, "IsStunned"), "A non-stun skipped action must clear the stun presentation.");
                RequireControlStars(unit, stars, false);
                RequireScheduledAttack(unit, time, time + .1f, time + 2f);
            }
            finally
            {
                Invoke(view, "SetUnitStunned", unit, false);
                Invoke(view, "FinishCharacterAttack", unit);
                try
                {
                    // 仅推进隔离视图的表现时钟，让施法层回到待机；不改全局 Time。
                    Write(view, "_presentationTime", time + 1f);
                    Invoke(view, "UpdateCharacterAnimations");
                }
                finally
                {
                    Write(view, "_presentationTime", time);
                }
                Invoke(view, "UpdateUnitControlStars");
            }
            Check(!Read<bool>(unit, "AttackPlaying"), "The isolated control test must finish its attack before other regressions.");
        }

        private static void RequireControlStars(object unit, SpriteRenderer[] prepared, bool visible)
        {
            var current = Read<SpriteRenderer[]>(unit, "StunStars");
            Check(current != null && current.Length == prepared.Length, "Control transitions must preserve prepared star instances.");
            for (int i = 0; i < prepared.Length; i++)
                Check(current[i] != null && current[i] == prepared[i] && current[i].sprite != null &&
                    current[i].enabled == visible,
                    "Prepared stun star " + i + " must be reused and " + (visible ? "enabled." : "hidden."));
        }

        private static void RequireScheduledAttack(object unit, float start, float release, float end)
        {
            Check(Read<bool>(unit, "AttackPlaying") && Read<Transform>(unit, "CastRoot") != null,
                "Changing stun presentation must not stop an already-started attack.");
            Check(Mathf.Abs(Read<float>(unit, "AttackBlendStart") - start) < .0001f &&
                Mathf.Abs(Read<float>(unit, "AttackReleaseAt") - release) < .0001f &&
                Mathf.Abs(Read<float>(unit, "AttackBlendEnd") - end) < .0001f,
                "Stun entry/recovery must preserve the exact scheduled start, release and end times.");
        }

        private static void VerifyLowHealth(BattleView view, string id)
        {
            var unit = Unit(view, id);
            // 固定在插值结束后的真实比例，避免刚受击的旧满血显示掩盖端点回归。
            Write(unit, "HealthBarFillRatio", 3f / 1208f);
            var design = Read<RectTransform>(view, "_referenceDesign");
            var scale = design.localScale;
            foreach (float factor in new[] { 1f, .5f })
            {
                design.localScale = scale * factor;
                Invoke(view, "RefreshReferenceHealth");
                var hud = Read<IDictionary>(view, "_referenceUnitHuds")[id];
                var fill = Read<RectTransform>(hud, "Fill");
                var corners = new Vector3[4];
                fill.GetWorldCorners(corners);
                float pixels = Vector2.Distance(RectTransformUtility.WorldToScreenPoint(null, corners[0]),
                    RectTransformUtility.WorldToScreenPoint(null, corners[3]));
                Check(fill.gameObject.activeInHierarchy && pixels >= 1.99f,
                    "Living unit with 3/1208 HP must retain at least 2 screen pixels of fill; got " + pixels + ".");
                var camera = Read<Camera>(view, "_battleCamera");
                var sprite = Read<SpriteRenderer>(unit, "Illustration");
                float headPixels = camera.WorldToScreenPoint(new Vector3(sprite.bounds.center.x, sprite.bounds.max.y, 0)).y;
                float fillBottomPixels = RectTransformUtility.WorldToScreenPoint(null, corners[0]).y;
                Check(fillBottomPixels > headPixels,
                    "The health bar must project above the visible sprite, not down onto the character's body.");
                var label = Read<TMP_Text>(hud, "Health");
                Check(label.gameObject.activeInHierarchy && label.enabled && label.color.a > 0f &&
                    label.text == $"{3:N0}/{1208:N0}", "Low-health label must display exact HP, not the minimum visual ratio.");
                Check(Read<Dictionary<string, long>>(view, "_unitHp")[id] == 3 &&
                    Read<Dictionary<string, long>>(view, "_unitMaxHp")[id] == 1208,
                    "Minimum visible fill must not alter authoritative health.");
            }
            design.localScale = scale;
        }

        private static void VerifyTargets(BattleView view, string actor, string first, string second, string dead)
        {
            var single = new BattleEvent { ActorId = actor, TargetId = second, Action = 801 };
            RequireTargets(view, single, second);
            var multi = new BattleEvent { ActorId = actor, TargetId = first, Action = 801 };
            multi.TargetIds.Add(new[] { first, first, second, dead, second, "" });
            RequireTargets(view, multi, first, second);
        }

        private static void VerifyTargetCancellation(BattleView view, string actor, string first, string second)
        {
            Invoke(view, "DispatchEvent", new BattleEvent
            {
                Type = BattleEventType.ActionCancelled, Action = 801, ActorId = actor,
                TargetId = first, Reason = BattleEventReason.TargetDead, HpBefore = 0, HpAfter = 0,
            });
            Check(Read<Dictionary<string, long>>(view, "_unitHp")[actor] == 671 &&
                !Read<bool>(Unit(view, actor), "DeathStarted"), "target_dead must not overwrite the live actor's 671 HP.");
            Check(!(bool)Invoke(view, "IsActionAborted", 801UL, actor),
                "One invalid AOE target must not abort the actor's whole action.");
            Check((bool)Invoke(view, "IsTargetActionAborted", 801UL, actor, first) &&
                !(bool)Invoke(view, "IsTargetActionAborted", 801UL, actor, second),
                "Only the cancelled target should be excluded from the in-flight action.");
            var remaining = new BattleEvent { ActorId = actor, TargetId = first, Action = 801 };
            remaining.TargetIds.Add(new[] { first, second, second });
            RequireTargets(view, remaining, second);
        }

        private static void VerifyLethalDamage(BattleView view, string actor, string victim)
        {
            var unit = Unit(view, victim);
            Invoke(view, "PlayCharacterAttack", unit, .1f, Read<float>(view, "_presentationTime") + 1f);
            Check(Read<bool>(unit, "AttackPlaying"), "Validation requires an active ink casting layer.");
            Invoke(view, "DispatchEvent", new BattleEvent
            {
                Type = BattleEventType.Damage, Action = 802, ActorId = actor, TargetId = victim,
                HpBefore = 200, HpAfter = 0, Amount = 200,
            });
            // 不等待帧或受击协程：血量归零的同一次调用必须停止出手并开始死亡。
            Check(Read<bool>(unit, "DeathStarted") && !Read<bool>(unit, "AttackPlaying"),
                "Lethal Damage must start death and stop attack before DispatchEvent returns.");
            var sprite = Read<SpriteRenderer>(unit, "Illustration");
            float deathAlpha = sprite.color.a;
            var lateHit = (IEnumerator)Invoke(view, "FlashHit", unit);
            Check(!lateHit.MoveNext() && Mathf.Approximately(sprite.color.a, deathAlpha),
                "A queued late hit reaction must not restore opacity or replay after death starts.");
            (lateHit as IDisposable)?.Dispose();
            Invoke(view, "RefreshReferenceHealth");
            var hud = Read<IDictionary>(view, "_referenceUnitHuds")[victim];
            Check(Read<RectTransform>(hud, "Fill").sizeDelta.x == 0f &&
                !Read<RectTransform>(hud, "Root").gameObject.activeSelf,
                "Dead units must not retain the living-unit minimum health fill or a visible HUD.");
            RequireTargets(view, new BattleEvent { ActorId = actor, TargetId = victim, Action = 803 });
        }

        private static void RequireTargets(BattleView view, BattleEvent action, params string[] expected)
        {
            var actual = new List<string>();
            foreach (var unit in (IEnumerable)Invoke(view, "CollectActionTargets", action))
                actual.Add(Read<string>(unit, "UnitId"));
            Check(actual.Count == expected.Length && new HashSet<string>(actual).SetEquals(expected),
                "Expected targets [" + string.Join(",", expected) + "]; got [" + string.Join(",", actual) + "].");
        }

        private static object Unit(BattleView view, string id) => Read<IDictionary>(view, "_unitVisuals")[id];
        private static T Read<T>(object target, string name) => (T)target.GetType().GetField(name, Fields).GetValue(target);
        private static void Write(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);
        private static object Invoke(BattleView view, string name, params object[] args)
        {
            try { return typeof(BattleView).GetMethod(name, Fields).Invoke(view, args); }
            catch (TargetInvocationException exception)
            {
                throw new InvalidOperationException(name + " failed during isolated validation.", exception.InnerException);
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class SavedView
        {
            private readonly BattleView _view;
            private readonly bool _active, _registered, _worldActive, _canBeginBattle;
            private readonly GameObject _world;
            private readonly Dictionary<string, Action<byte[]>> _handlers;
            private readonly Dictionary<string, Action<byte[]>> _savedHandlers = new();

            public SavedView(BattleView view, NetworkManager network)
            {
                _view = view;
                _handlers = Read<Dictionary<string, Action<byte[]>>>(network, "_handlers");
                foreach (string name in BattleMessages)
                    if (_handlers.TryGetValue(name, out var handler)) _savedHandlers.Add(name, handler);
                if (view == null) return;
                _active = view.gameObject.activeSelf;
                _registered = Read<bool>(view, "_registered");
                _world = Read<GameObject>(view, "_worldRoot");
                _worldActive = _world != null && _world.activeSelf;
                _canBeginBattle = Read<bool>(view, "_canBeginBattle");
            }

            public void Suspend()
            {
                if (_view == null) return;
                _view.gameObject.SetActive(false);
                Invoke(_view, "UnregisterNetworkHandlers");
            }

            public void Restore()
            {
                InstanceField.SetValue(null, _view);
                try
                {
                    if (_view == null) return;
                    // 抑制 OnEnable 自动注册，避免原本未注册的断线页面被重置。
                    Write(_view, "_registered", true);
                    _view.gameObject.SetActive(_active);
                    Write(_view, "_registered", false);
                    if (_registered) Invoke(_view, "RegisterNetworkHandlers");
                    if (_world != null) _world.SetActive(_worldActive);
                    Write(_view, "_canBeginBattle", _canBeginBattle);
                }
                finally
                {
                    // 保留原委托本身，也保留原本不存在的 handler；不影响其他消息类型。
                    foreach (string name in BattleMessages)
                    {
                        if (_savedHandlers.TryGetValue(name, out var handler)) _handlers[name] = handler;
                        else _handlers.Remove(name);
                    }
                }
            }
        }
    }
}
