using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XiuXian.Network;
using Q = XiuXian.Core.QuietInkUI;

namespace XiuXian.Core
{
    /// <summary>
    /// 独立战斗页面。服务端仍是唯一战斗权威；本类只消费
    /// BattleCreated/Action/Finished 推送，并以独立手绘图层表现飞行战斗。
    /// </summary>
    public sealed partial class BattleView : MonoBehaviour
    {
        private static BattleView _instance;
        private const float CharacterNormalizedHeight = 1.105f;

        // ---- 擂台站位 ----
        // 擂台是背景图里画出来的斜俯视圆盘。背景被 FitBattleBackground 拉伸铺满视口，
        // 所以图片 UV(左下角为原点)能线性换算成世界坐标。
        // 横屏用 block.png、竖屏用 back1.png，两张图的台面位置不同，必须分开配。
        private const float LandscapeArenaCenterU = 0.50f;
        private const float LandscapeArenaCenterV = 0.30f;
        private const float LandscapeArenaRadiusU = 0.46f;
        private const float LandscapeArenaRadiusV = 0.245f;

        private const float PortraitArenaCenterU = 0.50f;
        private const float PortraitArenaCenterV = 0.28f;
        private const float PortraitArenaRadiusU = 0.45f;
        private const float PortraitArenaRadiusV = 0.12f;

        // 阵型占用的擂台内场比例，留出外围台沿，避免角色脚底压到边框。
        private const float ArenaStandRadiusScale = 0.82f;
        // 台面透视：越靠前(屏幕下方)的单位越大，越靠后越小，这样才像站在同一个平面上。
        // 两个值都设成 1 就等于关掉透视缩放。
        private const float ArenaNearScale = 1.035f;
        private const float ArenaFarScale = 0.94f;
        // 每队独立按人数缩放；少人一方保留焦点，多人编队平缓缩小以免挤成团。
        private const float ArenaSoloScale = 1.42f;
        private const float ArenaMinUnitScale = 0.72f;
        private const float ArenaMaxUnitScale = 1.48f;
        private const float ArenaLeaderScale = 1.06f;
        private const float ArenaSupportScale = 0.97f;
        private const float HealthBarWidth = 0.68f;
        private const float HealthBarHeight = 0.064f;
        private const float AttackerHealthBarVerticalOffset = 1.27f;
        private const float DefenderHealthBarVerticalOffset = 1.27f;
        // 填充条每秒滚动的比例：满条约 0.67 秒走完，掉 20% 约 0.13 秒。
        private const float HealthBarFillSpeed = 1.5f;
        // 掉血后拖尾先停留这么久，再开始消退（秒）。
        private const float HealthBarDrainHoldSeconds = 0.22f;
        // 拖尾消退速度（每秒比例），明显慢于填充，才能看清"掉了多少"。
        private const float HealthBarDrainSpeed = 0.55f;
        private const float DeathFallDuration = 0.42f;
        private const float DeathFadeDuration = 0.48f;
        private Camera _battleCamera;
        private SpriteRenderer _battleBackground;
        // 按世界高度缓存的圆角胶囊贴图（key = 高度 * 1000）。所有单位共用。
        private readonly Dictionary<int, Sprite> _roundedBarSprites = new();
        private readonly Dictionary<int, Sprite> _skillFxSprites = new();
        private Sprite _landscapeBackgroundSprite;
        private Sprite _portraitBackgroundSprite;
        private bool _backgroundIsPortrait;
        private int _backgroundScreenWidth = -1;
        private int _backgroundScreenHeight = -1;

        private sealed class UnitVisual
        {
            public string UnitId;
            public bool IsDefender;
            public Transform Sphere;
            public Transform Body;
            public Renderer Renderer;
            public Color BaseColor;
            public Vector3 Home;
            public Vector3 BaseScale;
            public Vector3 BodyBaseLocalPosition;
            public Quaternion BodyBaseLocalRotation;
            public Vector3 BodyBaseLocalScale;
            public SpriteRenderer Illustration;
            public Transform CastRoot;
            public float AttackBlendStart;
            public float AttackReleaseAt;
            public float AttackBlendEnd;
            public bool AttackPlaying;
            public bool IsStunned;
            public float StunPoseWeight;
            public Transform ControlRoot;
            public SpriteRenderer[] StunStars;
            public Transform HealthBarRoot;
            public Transform HealthBarFill;
            public SpriteRenderer HealthBarFillRenderer;
            public Transform HealthBarGhost;
            public SpriteRenderer HealthBarGhostRenderer;
            public Transform HealthBarHighlight;
            public SpriteRenderer HealthBarHighlightRenderer;
            public SpriteRenderer CultivationSeal;
            public Vector3 CultivationSealBaseScale;
            // 血条动画状态：Fill 快速滚向目标值，Ghost 停留在掉血前的位置慢慢消退，
            // 两者的差值就是"这一段血正在流失"的白色拖尾。
            public float HealthBarTargetRatio = 1f;
            public float HealthBarFillRatio = 1f;
            public float HealthBarGhostRatio = 1f;
            public float HealthBarDrainHold;
            public bool DeathStarted;
            public Coroutine DeathRoutine;
            public Transform ReactionRoot;
            public float CastPoseUntil;
        }

        private readonly List<global::BattleEvent> _events = new();
        // 服务端判定作废的动作（攻击者出手前死亡 / 目标在出手前死亡）。
        // 这些动作的演出必须中止，否则会出现"已经死了还打一下"。
        private readonly HashSet<ulong> _cancelledActions = new();
        private readonly HashSet<(ulong Action, string Target)> _cancelledActionTargets = new();
        private BattleTimelinePlanner _timelinePlanner = new();
        private readonly UnitMotionQueue<global::BattleEvent> _motionQueue = new();
        private readonly Dictionary<string, Queue<UnitVisual>> _reactionQueues = new();
        private readonly HashSet<string> _reactionActive = new();
        private readonly Dictionary<string, UnitVisual> _unitVisuals = new();
        private readonly HashSet<GameObject> _transientEffects = new();
        private readonly List<UnitVisual> _attackers = new();
        private readonly List<UnitVisual> _defenders = new();
        private readonly Dictionary<string, long> _unitHp = new();
        private readonly Dictionary<string, long> _unitMaxHp = new();
        private GameObject _worldRoot;
        private TMP_FontAsset _uiFont;
        private Texture2D _skillFxAtlas;
        private Texture2D _beamTrailTexture;
        private Material _beamMaterial;
        private TMP_Text _battleStatusText;
        private TMP_Text _roundText;
        private GameObject _resultPage;
        private bool _canBeginBattle = true;
        private RectTransform _battleHeaderRect;
        private RectTransform _battleCommandDockRect;
        private RectTransform _playerCardRect;
        private RectTransform _enemyCardRect;
        private RectTransform _battleActionsRect;
        private TMP_Text _playerHealthText;
        private TMP_Text _enemyHealthText;
        private Image _playerHealthFill;
        private Image _enemyHealthFill;
        private int _lastHudScreenWidth = -1;
        private int _lastHudScreenHeight = -1;        private float _lastHudRootWidth = -1f;
        private float _lastHudRootHeight = -1f;

        private Rect _lastHudSafeArea;

        private Coroutine _playRoutine;
        private Coroutine _battleCreateTimeout;
        private global::BattleFinishedNtf _pendingFinished;
        private string _battleId;
        private float _tickDurationSeconds = 0.1f;
        private float _timelineEpoch;
        private int _runningAnimations;
        private bool _registered;
        private Vector3 _cameraHomePosition;

public static void Show(Transform canvasRoot)
        {
            if (ResumeExisting()) return;
            var canvas = canvasRoot != null ? canvasRoot.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
            {
                Debug.LogError("[BattleView] 无法打开战斗页面：入口不在 Canvas 下。");
                return;
            }
            canvasRoot = canvas.transform;
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<BattleView>(FindObjectsInactive.Include);
            }
            if (_instance != null && (_instance.transform.Find("BattleQuietInkV2") == null ||
                                      _instance._skillLabels.Count == 0))
            {
                var staleView = _instance.gameObject;
                _instance = null;
                staleView.SetActive(false);
                Destroy(staleView);
            }
            if (_instance == null)
            {
                var go = new GameObject("BattleView", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(canvasRoot, false);
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                var background = go.GetComponent<Image>();
                background.color = Color.clear;
                background.raycastTarget = true;
                _instance = go.AddComponent<BattleView>();
            }
            else if (_instance.transform.parent != canvasRoot)
            {
                _instance.transform.SetParent(canvasRoot, false);
            }

            UIManager.Instance.RegisterScreen(UIScreen.Battle, _instance.gameObject);
            UIManager.Instance.Show(UIScreen.Battle);
            _instance.transform.SetAsLastSibling();
            _instance.BeginBattle();
        }

private void Awake()
        {
            _instance = this;
            _uiFont = TMP_Settings.defaultFontAsset;
            WarmupControlFx();
            var versionMarker = new GameObject("BattleQuietInkV2");
            versionMarker.transform.SetParent(transform, false);
            BuildWorld();
            BuildUi();
        }

        private void OnEnable()
        {
            _instance = this;
            if (_worldRoot != null)
            {
                _worldRoot.SetActive(!_isBattleMinimized);
            }
            RegisterNetworkHandlers();
        }

        private void OnDisable()
        {
            CloseBattleResponseGate();
            _hasBattleSession = false;
            _isBattleMinimized = false;
            _battleIsFinished = false;
            _pendingFinished = null;
            _battleId = string.Empty;
            _events.Clear();
            _canBeginBattle = true;
            UnregisterNetworkHandlers();
            StopAllCoroutines();
            _battleCreateTimeout = null;
            _runningAnimations = 0;
            _playRoutine = null;
            _reactionQueues.Clear();
            _reactionActive.Clear();
            _motionQueue.Clear();
            ClearTransientEffects();
            foreach (var visual in _unitVisuals.Values)
            {
                StopCharacterAttack(visual);
            }
            if (_battleCamera != null)
            {
                _battleCamera.transform.localPosition = _cameraHomePosition;
            }
            if (_worldRoot != null)
            {
                _worldRoot.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            ReleaseControlFx();
            ReleaseBattleEnvironments();
            UnregisterNetworkHandlers();
            ClearTransientEffects();
            if (_worldRoot != null)
            {
                Destroy(_worldRoot);
            }
            foreach (var sprite in _roundedBarSprites.Values)
            {
                if (sprite == null)
                {
                    continue;
                }
                if (sprite.texture != null)
                {
                    Destroy(sprite.texture);
                }
                Destroy(sprite);
            }
            _roundedBarSprites.Clear();
            foreach (var sprite in _skillFxSprites.Values)
            {
                if (sprite != null) Destroy(sprite);
            }
            _skillFxSprites.Clear();
            if (_beamMaterial != null) Destroy(_beamMaterial);
            if (_beamTrailTexture != null) Destroy(_beamTrailTexture);
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void RegisterNetworkHandlers()
        {
            if (_registered)
            {
                return;
            }
            var net = NetworkManager.Instance;
            net.RegisterProtoHandler("StartBattleAck", global::StartBattleAck.Parser, OnStartBattleAck);
            net.RegisterProtoHandler("BattleCreatedNtf", global::BattleCreatedNtf.Parser, OnBattleCreated);
            net.RegisterProtoHandler("BattleActionNtf", global::BattleActionNtf.Parser, OnBattleAction);
            net.RegisterProtoHandler("BattleFinishedNtf", global::BattleFinishedNtf.Parser, OnBattleFinished);
            SubscribeBattleConnection(net);
            _registered = true;
        }

        private void UnregisterNetworkHandlers()
        {
            UnsubscribeBattleConnection();
            if (!_registered || NetworkManager.Instance == null)
            {
                return;
            }
            var net = NetworkManager.Instance;
            net.UnregisterHandler("StartBattleAck");
            net.UnregisterHandler("BattleCreatedNtf");
            net.UnregisterHandler("BattleActionNtf");
            net.UnregisterHandler("BattleFinishedNtf");
            _registered = false;
        }

        private void BeginBattle()
        {
            // 创建请求和当前战斗尚未结束时，不能清空本地播放状态再次开战。
            if (!_canBeginBattle) return;
            _canBeginBattle = false;
            BeginBattleResponseGate();
            _hasBattleSession = true;
            _battleIsFinished = false;
            SetScreenVisible(true);
            ClearBattleRecoveryControl();
            if (_resultPage != null)
            {
                _resultPage.SetActive(false);
                Destroy(_resultPage);
                _resultPage = null;
            }
            RegisterNetworkHandlers();
            StopAllCoroutines();
            _battleCreateTimeout = null;
            _playRoutine = null;
            _runningAnimations = 0;
            _events.Clear();
            _cancelledActions.Clear();
            _cancelledActionTargets.Clear();
            _timelinePlanner = new BattleTimelinePlanner();
            _motionQueue.Clear();
            _reactionQueues.Clear();
            _reactionActive.Clear();
            _pendingFinished = null;
            _battleId = string.Empty;
            _tickDurationSeconds = 0.1f;
            _timelineEpoch = PresentationTime;
            ClearUnitVisuals();
            SelectBattleEnvironment(true);
            RefreshTeamHealthHud();
            var net = NetworkManager.Instance;
            if (net == null || !net.IsConnected)
            {
                _canBeginBattle = true;
                SetBattleStatus("服务器未连接，请返回登录后重试", "连接断开");
                ShowBattleRecoveryControl(returnToLogin: true);
                ClearFailedBattleCreation();
                return;
            }
            SetBattleStatus("正在召唤战场…", "备战");

            _startBattleRequestCount++;
            net.SendProto(new global::StartBattleReq
            {
                BattleType = global::BattleType.Test,
                BusinessId = "unity-two-sphere-demo",
                MonsterGroupConfigId = 1,
            });
            _battleCreateTimeout = StartCoroutine(WaitForBattleCreated());
        }

        private IEnumerator WaitForBattleCreated()
        {
            yield return new WaitForSecondsRealtime(12f);
            _battleCreateTimeout = null;
            FailBattleResponse("等待战斗数据超时，请重试", "等待超时");
        }
        private void OnStartBattleAck(global::StartBattleAck ack)
        {
            if (!_awaitingStartAck) return;
            if (ack.Code != global::ErrCode.Ok)
            {
                Debug.LogWarning($"[BattleView] 创建战斗失败: code={ack.Code}, battleId={ack.BattleId}");
                bool timedOut = ack.Code == global::ErrCode.BattleTimeout;
                FailBattleResponse(LoadoutErrors.ToUserMessage(ack.Code),
                    timedOut ? "创建超时" : "创建失败");
                return;
            }
            if (!string.IsNullOrEmpty(ack.BattleId))
            {
                _battleId = ack.BattleId;
            }
            AcceptBattleResponse();
        }
        private void OnBattleCreated(global::BattleCreatedNtf message)
        {
            if (BufferUntilStartAck(() => OnBattleCreated(message))) return;
            if (!string.IsNullOrEmpty(_battleId) && _battleId != message.BattleId) return;
            if (string.IsNullOrEmpty(message.BattleId))
            {
                return;
            }
            CancelBattleCreateTimeout();
            _battleId = message.BattleId;
            _tickDurationSeconds = Mathf.Max(0.001f,
                (message.TickDurationMs > 0 ? message.TickDurationMs : 100) / 1000f);
            _timelineEpoch = PresentationTime;
            SetBattleStatus("天道演武 · 战斗开始", "时序 0");
            _canBeginBattle = false;
            ClearBattleRecoveryControl();

            // 参战名单随创建通知下发：进战斗页就把双方单位摆好，并预置真实血量上限。
            // 不能只靠事件懒创建 —— 那样要等某个单位先行动，对面会一直空着。
            foreach (var unit in message.Units)
            {
                if (unit == null || string.IsNullOrEmpty(unit.InstanceId))
                {
                    continue;
                }
                var visual = EnsureUnitVisual(unit.InstanceId,
                    unit.Team == global::BattleTeam.Defender);
                _unitHp[unit.InstanceId] = System.Math.Max(0L, unit.Hp);
                _unitMaxHp[unit.InstanceId] = System.Math.Max(0L, unit.MaxHp);
                UpdateHealthBar(visual);
                ApplyUnitLifeVisual(visual);
            }
            RefreshTeamHealthHud();
        }

        private void CancelBattleCreateTimeout()
        {
            if (_battleCreateTimeout == null) return;
            StopCoroutine(_battleCreateTimeout);
            _battleCreateTimeout = null;
        }
        private void OnBattleAction(global::BattleActionNtf message)
        {
            if (BufferUntilStartAck(() => OnBattleAction(message))) return;
            if (message.Event == null || string.IsNullOrEmpty(message.BattleId))
            {
                return;
            }
            if (!string.IsNullOrEmpty(_battleId) && _battleId != message.BattleId)
            {
                return;
            }
            if (_timelinePlanner.IsSealed)
            {
                Debug.LogError($"[BattleView] BattleFinished 后收到迟到事件 seq={message.Event.Sequence}");
                return;
            }

            _battleId = message.BattleId;
            string actionLabel = message.Event.Type switch
            {
                global::BattleEventType.SkillUsed => "灵诀释放",
                global::BattleEventType.Damage => "锋芒命中",
                global::BattleEventType.Heal => "灵息回复",
                global::BattleEventType.ActionCancelled => "招式中断",
                _ => "双方交锋中",
            };
            // Status and skill slots update when the queued event is actually presented.
            EnsureEventUnits(message.Event);
            RecordTimelineMarker(message.Event);
            _events.Add(message.Event);
            EnsurePlaybackStarted(message.Event.Tick);
        }

        private void OnBattleFinished(global::BattleFinishedNtf message)
        {
            if (BufferUntilStartAck(() => OnBattleFinished(message))) return;
            if (!string.IsNullOrEmpty(_battleId) && _battleId != message.BattleId)
            {
                return;
            }
            if (_timelinePlanner.IsSealed)
            {
                return;
            }

            _battleId = message.BattleId;
            foreach (var unit in message.Units)
            {
                bool isDefender = unit.Team == global::BattleTeam.Defender;
                EnsureUnitVisual(unit.InstanceId, isDefender);
            }
            _pendingFinished = message;

            // Finished 只表示服务端不再发送 action。已完整的 action 在此之前已经
            // 流式播放；这里只校验是否残留不完整 timing，并等待队列自然排空。
            if (!_timelinePlanner.TrySeal(out var timingError))
            {
                Debug.LogError($"[BattleView] 战报结束时仍有无效 timing，相关 motion 将跳过: {timingError}");
            }
            if (_events.Count > 0)
            {
                EnsurePlaybackStarted(_events[0].Tick);
            }
            TryApplyFinished();
        }

        private static int CompareEvents(global::BattleEvent left, global::BattleEvent right)
        {
            int tickOrder = left.Tick.CompareTo(right.Tick);
            return tickOrder != 0 ? tickOrder : left.Sequence.CompareTo(right.Sequence);
        }

        private void RecordTimelineMarker(global::BattleEvent battleEvent)
        {
            var kind = battleEvent.Type switch
            {
                global::BattleEventType.ActionStarted => BattleTimelineMarkerKind.ActionStarted,
                global::BattleEventType.SkillUsed => BattleTimelineMarkerKind.SkillUsed,
                global::BattleEventType.ActionEnded => BattleTimelineMarkerKind.ActionEnded,
                global::BattleEventType.ActionCancelled => BattleTimelineMarkerKind.Impact,
                global::BattleEventType.Damage when !string.IsNullOrEmpty(battleEvent.SkillId) =>
                    BattleTimelineMarkerKind.Impact,
                global::BattleEventType.Heal when !string.IsNullOrEmpty(battleEvent.SkillId) =>
                    BattleTimelineMarkerKind.Impact,
                global::BattleEventType.StatusApplied when !string.IsNullOrEmpty(battleEvent.SkillId) =>
                    BattleTimelineMarkerKind.Impact,
                _ => BattleTimelineMarkerKind.Other,
            };
            _timelinePlanner.Add(battleEvent.Action, battleEvent.Tick, kind, battleEvent.ActorId);

            // 服务端已在 SKILL_USED 上带齐动作窗口，收到这一条就能落窗起播，
            // 不必等动作结束的 ActionEnded（流式下发时它会晚整整一个动作到达）。
            // 旧服务端不带这三个字段，此时回落到原有的等待路径。
            if (kind == BattleTimelineMarkerKind.SkillUsed &&
                battleEvent.ImpactTick > 0 && battleEvent.EndTick > 0)
            {
                _timelinePlanner.SeedMotionWindow(battleEvent.Action, battleEvent.ActorId,
                    battleEvent.StartTick, battleEvent.ImpactTick, battleEvent.EndTick);
            }
        }

        private void EnsurePlaybackStarted(long firstTick)
        {
            if (_playRoutine != null)
            {
                return;
            }
            // 将第一条已收到事件锚定到“现在”，后续事件继续按服务端 tick 间隔播放。
            _timelineEpoch = PresentationTime -
                System.Math.Max(0L, firstTick) * _tickDurationSeconds;
            _playRoutine = StartCoroutine(PlayEvents());
        }

        private IEnumerator PlayEvents()
        {
            yield return null;
            while (true)
            {
                if (_events.Count == 0)
                {
                    if (_timelinePlanner.IsSealed)
                    {
                        break;
                    }
                    yield return null;
                    continue;
                }

                _events.Sort(CompareEvents);
                var battleEvent = _events[0];
                if (battleEvent.Type == global::BattleEventType.SkillUsed &&
                    !_timelinePlanner.TryGetActionWindow(battleEvent.Action, out _))
                {
                    if (_timelinePlanner.IsSealed)
                    {
                        Debug.LogError($"[BattleView] action={battleEvent.Action} timing 未完整，跳过 motion");
                        _events.RemoveAt(0);
                        continue;
                    }
                    // ActionEnded 可能在后续网络帧到达；窗口完整后立刻继续。
                    yield return null;
                    continue;
                }

                float dueTime = _timelineEpoch + battleEvent.Tick * _tickDurationSeconds;
                if (PresentationTime < dueTime)
                {
                    // 每帧重新排序，允许较早 tick 的迟到网络事件插入正确位置。
                    yield return null;
                    continue;
                }
                _events.RemoveAt(0);
                DispatchEvent(battleEvent);
            }
            _playRoutine = null;
            TryApplyFinished();
        }

        private void DispatchEvent(global::BattleEvent battleEvent)
        {
            RecordReferenceEvent(battleEvent);
            switch (battleEvent.Type)
            {
                case global::BattleEventType.ActionStarted:
                    UpdateHealth(battleEvent.ActorId, battleEvent.HpAfter, battleEvent.HpBefore);
                    break;
                case global::BattleEventType.SkillUsed:
                    SetUnitStunned(EnsureUnitVisual(battleEvent.ActorId), false);
                    EnqueueMotionAction(battleEvent);
                    break;
                case global::BattleEventType.Damage:
                {
                    var targetVisual = EnsureUnitVisual(battleEvent.TargetId);
                    UpdateHealth(battleEvent.TargetId, battleEvent.HpAfter, battleEvent.HpBefore);
                    long damage = System.Math.Max(0L, battleEvent.HpBefore - battleEvent.HpAfter);
                    if (damage > 0) StartTracked(AnimateFloatingNumber(targetVisual, damage, false));
                    EnqueueHitReaction(targetVisual);
                    if (battleEvent.EffectType == global::EffectType.IceBurst)
                        StartTracked(AnimateChargedEffect(targetVisual, true));
                    else if (battleEvent.EffectType == global::EffectType.Thunder)
                        StartTracked(AnimateChargedEffect(targetVisual, false));
                    break;
                }
                case global::BattleEventType.Heal:
                {
                    var targetVisual = EnsureUnitVisual(battleEvent.TargetId);
                    UpdateHealth(battleEvent.TargetId, battleEvent.HpAfter, battleEvent.HpBefore);
                    long healing = System.Math.Max(0L, battleEvent.HpAfter - battleEvent.HpBefore);
                    if (healing > 0) StartTracked(AnimateFloatingNumber(targetVisual, healing, true));
                    if (healing > 0) StartTracked(AnimateCombatPulse(targetVisual,
                        new Color(0.36f, 1f, 0.64f, 1f), null));
                    ApplyUnitLifeVisual(targetVisual);
                    break;
                }
                case global::BattleEventType.StatusApplied:
                {
                    var targetVisual = EnsureUnitVisual(battleEvent.TargetId);
                    bool stun = BattleStatusPresentationCatalog.IsStunStatus(battleEvent.StatusId);
                    if (stun) SetUnitStunned(targetVisual, true);
                    StartTracked(AnimateCombatPulse(targetVisual, stun ? StunTint :
                        new Color(0.42f, 0.84f, 1f, 1f), stun ? "眩晕" : null));
                    break;
                }
                case global::BattleEventType.ActionSkipped:
                    SetUnitStunned(EnsureUnitVisual(battleEvent.ActorId),
                        battleEvent.Reason == global::BattleEventReason.Stunned);
                    break;
                case global::BattleEventType.ActionCancelled:
                {
                    if (battleEvent.Reason == global::BattleEventReason.ActorDead)
                    {
                        _cancelledActions.Add(battleEvent.Action);
                        UpdateHealth(battleEvent.ActorId, battleEvent.HpAfter, battleEvent.HpBefore);
                    }
                    else if (!string.IsNullOrEmpty(battleEvent.TargetId))
                    {
                        // target_dead 的 HP 属于目标；群攻中一个目标失效不取消其他目标。
                        _cancelledActionTargets.Add((battleEvent.Action, battleEvent.TargetId));
                        if (battleEvent.Reason == global::BattleEventReason.TargetDead)
                            UpdateHealth(battleEvent.TargetId, battleEvent.HpAfter, battleEvent.HpBefore);
                    }
                    else
                    {
                        _cancelledActions.Add(battleEvent.Action);
                    }
                    break;
                }
            }
        }

        private bool IsUnitDefeated(string unitId) =>
            !string.IsNullOrEmpty(unitId) &&
            _unitHp.TryGetValue(unitId, out var hp) && hp <= 0;

        private bool IsActionAborted(ulong action, string actorId) =>
            _cancelledActions.Contains(action) || IsUnitDefeated(actorId);

        private bool IsTargetActionAborted(ulong action, string actorId, string targetId) =>
            IsActionAborted(action, actorId) || IsUnitDefeated(targetId) ||
            _cancelledActionTargets.Contains((action, targetId));
        private void StartTracked(IEnumerator animation)
        {
            if (animation == null)
            {
                return;
            }
            _runningAnimations++;
            StartCoroutine(RunTracked(animation));
        }

        private IEnumerator RunTracked(IEnumerator animation)
        {
            var pending = new Stack<IEnumerator>();
            pending.Push(animation);
            try
            {
                // Advance nested iterators here so exceptions unwind our finally,
                // instead of leaving the animation count owned by Unity's runner.
                while (pending.Count > 0)
                {
                    var current = pending.Peek();
                    if (!current.MoveNext())
                    {
                        pending.Pop();
                        (current as System.IDisposable)?.Dispose();
                        continue;
                    }
                    if (current.Current is IEnumerator nested)
                        pending.Push(nested);
                    else
                        yield return current.Current;
                }
            }
            finally
            {
                try
                {
                    while (pending.Count > 0)
                        (pending.Pop() as System.IDisposable)?.Dispose();
                }
                finally
                {
                    _runningAnimations = Mathf.Max(0, _runningAnimations - 1);
                }
            }
            // Cancellation or an exception must never manufacture a result.
            TryApplyFinished();
        }

        private void EnqueueMotionAction(global::BattleEvent skillEvent)
        {
            if (!_timelinePlanner.TryGetActionWindow(skillEvent.Action, out var window))
            {
                Debug.LogError($"[BattleView] action={skillEvent.Action} 没有完整 timing，拒绝启动 motion");
                return;
            }
            if (_motionQueue.Enqueue(window.ActorId, skillEvent))
            {
                // 每个 unit 只启动这一个 worker；队列后续动作不会创建新的 Transform owner。
                StartTracked(PlayMotionTrack(window.ActorId));
            }
        }

        private IEnumerator PlayMotionTrack(string unitId)
        {
            while (_motionQueue.TryTake(unitId, out var skillEvent))
            {
                yield return AnimateAction(skillEvent);
            }
            _motionQueue.CompleteOwner(unitId);
        }

        private IEnumerator AnimateAction(global::BattleEvent skillEvent)
        {
            if (!_timelinePlanner.TryGetActionWindow(skillEvent.Action, out var timing))
            {
                yield break;
            }
            // 动作已被判定作废，或攻击者在收到这条事件时就已经倒下：不演出。
            if (IsActionAborted(skillEvent.Action, skillEvent.ActorId))
            {
                yield break;
            }

            var attacker = EnsureUnitVisual(skillEvent.ActorId);
            var targets = CollectActionTargets(skillEvent);
            if (attacker == null || targets.Count == 0)
            {
                yield break;
            }
            var target = targets[0];

            // 网络包可能排队晚到。用服务端时间轴计算剩余前摇，过了命中时刻就丢弃，
            // 避免血量已经结算完毕后又补播一整段攻击。
            float impactDeadline = _timelineEpoch + timing.ImpactTick * _tickDurationSeconds;
            float endDeadline = _timelineEpoch + timing.EndTick * _tickDurationSeconds;
            float windupSeconds = impactDeadline - PresentationTime;
            if (windupSeconds <= 0f)
            {
                yield break;
            }
            float recoverySeconds = Mathf.Max(0f, endDeadline - impactDeadline);
            float beamTravelSeconds = Mathf.Min(windupSeconds,
                Mathf.Clamp(windupSeconds * 0.5f, 0.035f, 0.18f));
            float preCastSeconds = Mathf.Max(0f, windupSeconds - beamTravelSeconds);
            float fadeSeconds = Mathf.Clamp(recoverySeconds, 0f, 0.14f);
            bool stunSkill = BattleStatusPresentationCatalog.SkillAppliesStun(skillEvent.SkillId);

            // 施法图层对齐服务器窗口；弹道抵达仍严格对应真实命中时刻。
            PlayCharacterAttack(attacker, preCastSeconds, endDeadline);
            attacker.CastPoseUntil = PresentationTime + windupSeconds + recoverySeconds;
            if (preCastSeconds > 0f)
            {
                if (stunSkill)
                    yield return AnimateStunCharge(attacker, target, preCastSeconds, skillEvent.Action);
                else
                    yield return FlyWithinFormation(attacker, preCastSeconds, skillEvent.Action);
            }
            // 前摇结束正是服务端结算 impact 的时刻：此刻若攻击者已倒下或动作被作废，
            // 这一击就不该打出去。AnimateBlueBeam 还会在飞行途中继续兜底。
            targets.RemoveAll(unit => IsTargetActionAborted(skillEvent.Action, skillEvent.ActorId, unit.UnitId));
            if (IsActionAborted(skillEvent.Action, skillEvent.ActorId) || targets.Count == 0)
            {
                FinishCharacterAttack(attacker);
                yield break;
            }

            yield return AnimateSkillProjectiles(attacker, targets, beamTravelSeconds, fadeSeconds,
                skillEvent.Action, stunSkill);
            float remainingRecovery = Mathf.Max(0f, endDeadline - PresentationTime);
            if (remainingRecovery > 0f)
            {
                yield return FlyBackHome(attacker, remainingRecovery);
            }

            FinishCharacterAttack(attacker);
        }

        private IEnumerator FlyWithinFormation(UnitVisual attacker, float duration, ulong action)
        {
            var ribbon = CreateFlightRibbon(attacker);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (IsActionAborted(action, attacker.UnitId)) break;
                elapsed += PresentationDelta;
                UpdateFlightRibbon(ribbon, attacker);
                yield return null;
            }
            DestroyTransientEffect(ribbon.transform.parent.gameObject);
        }

        private Vector3 AirCastPosition(UnitVisual unit)
        {
            float phase = Mathf.Abs(unit.UnitId.GetHashCode() % 1000) * .01f;
            float drift = Mathf.Lerp(1f, .65f, unit.StunPoseWeight);
            return unit.Home + new Vector3(Mathf.Sin(PresentationTime * 1.1f + phase) * .28f,
                Mathf.Cos(PresentationTime * 1.4f + phase) * .16f, 0f) * drift;
        }

        private IEnumerator FlyBackHome(UnitVisual unit, float duration)
        {
            var ribbon = CreateFlightRibbon(unit);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (unit.Sphere == null || unit.DeathStarted) break;
                elapsed += PresentationDelta;
                UpdateFlightRibbon(ribbon, unit);
                yield return null;
            }
            DestroyTransientEffect(ribbon.transform.parent.gameObject);
        }

        private LineRenderer CreateFlightRibbon(UnitVisual unit)
        {
            var root = new GameObject("FlightRibbon-" + unit.UnitId);
            root.transform.SetParent(_worldRoot.transform, false);
            _transientEffects.Add(root);
            var line = CreateBeamLine(root.transform, "Slipstream", .055f,
                unit.IsDefender ? new Color(.73f, .39f, .30f, .28f) : new Color(.20f, .50f, .47f, .32f));
            line.positionCount = 12;
            for (int i = 0; i < 12; i++) line.SetPosition(i, unit.Sphere.position);
            return line;
        }

        private void UpdateFlightRibbon(LineRenderer ribbon, UnitVisual unit)
        {
            for (int i = 0; i < ribbon.positionCount - 1; i++)
                ribbon.SetPosition(i, ribbon.GetPosition(i + 1));
            ribbon.SetPosition(ribbon.positionCount - 1, unit.Sphere.position + Vector3.up * .35f);
        }

        private void EnqueueHitReaction(UnitVisual visual)
        {
            if (visual == null || visual.DeathStarted || IsUnitDefeated(visual.UnitId))
            {
                return;
            }
            if (!_reactionQueues.TryGetValue(visual.UnitId, out var queue))
            {
                queue = new Queue<UnitVisual>();
                _reactionQueues.Add(visual.UnitId, queue);
            }
            if (queue.Count == 0) queue.Enqueue(visual);
            if (_reactionActive.Add(visual.UnitId))
            {
                StartTracked(PlayHitReactions(visual.UnitId));
            }
        }

        private IEnumerator PlayHitReactions(string unitId)
        {
            var queue = _reactionQueues[unitId];
            while (queue.Count > 0)
            {
                var visual = queue.Dequeue();
                yield return FlashHit(visual);
                ApplyUnitLifeVisual(visual);
            }
            _reactionActive.Remove(unitId);
        }

        private void TryApplyFinished()
        {
            if (_pendingFinished == null || _events.Count > 0 || _playRoutine != null ||
                _runningAnimations > 0 || _motionQueue.ActiveOwnerCount > 0)
            {
                return;
            }
            var finished = _pendingFinished;
            _pendingFinished = null;
            ApplyFinished(finished);
        }

        private IEnumerator HoldVisualAtHome(UnitVisual visual, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                KeepVisualAtHome(visual);
                elapsed += PresentationDelta;
                yield return null;
            }
            KeepVisualAtHome(visual);
        }

        private void KeepVisualAtHome(UnitVisual visual)
        {
            if (visual?.Sphere == null || visual.Body == null)
            {
                return;
            }
            visual.Sphere.position = visual.Home;
            visual.Body.localPosition = visual.BodyBaseLocalPosition;
            visual.Body.localRotation = visual.BodyBaseLocalRotation;
            bool alive = !_unitHp.TryGetValue(visual.UnitId, out var hp) || hp > 0;
            visual.Body.localScale = visual.BodyBaseLocalScale * (alive ? 1f : 0.72f);
        }

        private IEnumerator AnimateCastCharge(UnitVisual attacker, float duration, ulong action)
        {
            var root = new GameObject($"CastCharge-{attacker.UnitId}");
            root.transform.SetParent(_worldRoot.transform, false);
            _transientEffects.Add(root);
            var sigil = CreateFxRenderer(root.transform, "CastingSigil", 0, 0, 0.24f, 75);
            if (sigil == null)
            {
                yield return HoldVisualAtHome(attacker, duration);
                DestroyTransientEffect(root);
                yield break;
            }

            float elapsed = 0f;
            duration = Mathf.Max(0.08f, duration);
            while (elapsed < duration)
            {
                KeepVisualAtHome(attacker);
                if (IsActionAborted(action, attacker.UnitId))
                {
                    DestroyTransientEffect(root);
                    yield break;
                }
                elapsed += PresentationDelta;
                float t = Mathf.Clamp01(elapsed / duration);
                float pulse = 0.96f + Mathf.Sin(t * Mathf.PI * 5f) * 0.04f;
                sigil.transform.position = attacker.Home + new Vector3(0f, 0.04f, -0.65f);
                sigil.transform.localScale = Vector3.one * Mathf.Lerp(0.22f, 0.48f, Mathf.SmoothStep(0f, 1f, t)) * pulse;
                sigil.transform.Rotate(0f, 0f, -82f * PresentationDelta);
                Color sealTint = attacker.IsDefender
                    ? new Color(1f, 0.30f, 0.52f, 1f)
                    : new Color(0.56f, 0.88f, 1f, 1f);
                sealTint.a = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 4f));
                sigil.color = sealTint;
                yield return null;
            }
            DestroyTransientEffect(root);
        }

        private IEnumerator AnimateBlueBeam(UnitVisual attacker, UnitVisual target,
            float travelDuration, float fadeDuration, ulong action, bool stunSkill = false)
        {
            var effectRoot = new GameObject($"SkillFx-{attacker.UnitId}-{target.UnitId}");
            effectRoot.transform.SetParent(_worldRoot.transform, false);
            _transientEffects.Add(effectRoot);

            Color trailTint = attacker.IsDefender
                ? (_defenders.IndexOf(attacker) % 3 == 0 ? Q.Blue : Q.Cinnabar)
                : Q.Jade;
            if (stunSkill) trailTint = StunTint;
            Color secondaryTint = new Color(Q.Gold.r, Q.Gold.g, Q.Gold.b, .24f);
            var glow = CreateBeamLine(effectRoot.transform, "AuraTrail", .09f,
                new Color(trailTint.r, trailTint.g, trailTint.b, .46f));
            var core = CreateBeamLine(effectRoot.transform, "CoreTrail", .026f,
                Color.Lerp(trailTint, Color.white, .42f));
            var projectile = stunSkill
                ? CreateControlFx(effectRoot.transform, "StunProjectileCore", false, .42f, 85)
                : CreateFxRenderer(effectRoot.transform, "ProjectileCore", 1, 0, 0.42f, 85);
            var swordWave = stunSkill
                ? CreateControlFx(effectRoot.transform, "StunSigil", true, .72f, 88)
                : CreateFxRenderer(effectRoot.transform, "SwordWave", 1, 1, .72f, 88);
            if (swordWave != null) swordWave.color = Color.clear;
            var echoLeft = CreateBeamLine(effectRoot.transform, "EchoLeft", .02f, new Color(trailTint.r, trailTint.g, trailTint.b, .32f));
            var echoRight = CreateBeamLine(effectRoot.transform, "EchoRight", 0.025f, secondaryTint);

            int curveSeed = unchecked((attacker.UnitId?.GetHashCode() ?? 0) * 397 ^
                                      (target.UnitId?.GetHashCode() ?? 0) ^
                                      Time.frameCount ^ System.Environment.TickCount);
            var curveRandom = new System.Random(curveSeed);
            float curveSide = curveRandom.NextDouble() < 0.5 ? -1f : 1f;
            float firstBend = curveSide * Mathf.Lerp(0.12f, 0.22f,
                (float)curveRandom.NextDouble());
            float secondBend = -curveSide * Mathf.Lerp(0.08f, 0.18f,
                (float)curveRandom.NextDouble());

            travelDuration = Mathf.Max(0.01f, travelDuration);
            float elapsed = 0f;
            while (elapsed < travelDuration)
            {
                // 目标可能恰被这次命中击杀，不能据此吞掉本次命中特效；失效目标以取消事件为准。
                if (IsActionAborted(action, attacker.UnitId) ||
                    _cancelledActionTargets.Contains((action, target.UnitId)))
                {
                    DestroyTransientEffect(effectRoot);
                    yield break;
                }

                elapsed += PresentationDelta;
                float t = Mathf.Clamp01(elapsed / travelDuration);
                float eased = t * t * (3f - 2f * t);
                Vector3 origin = GetBeamOrigin(attacker, target);
                Vector3 destination = GetBeamTarget(target);
                SetBeamCurvePositions(glow, core, origin, destination, eased, firstBend, secondBend);
                SetSingleTrail(echoLeft, origin, destination, eased, firstBend + 0.08f, secondBend - 0.06f);
                SetSingleTrail(echoRight, origin, destination, Mathf.Max(0f, eased - 0.06f), firstBend - 0.07f, secondBend + 0.08f);
                ScrollTrailTexture(glow, core, echoLeft, echoRight);
                if (projectile != null)
                {
                    projectile.transform.position = core.GetPosition(core.positionCount - 1);
                    projectile.transform.Rotate(0f, 0f, 420f * PresentationDelta);
                    float shellScale = 0.30f + Mathf.Sin(PresentationTime * 12f) * 0.018f;
                    projectile.transform.localScale = Vector3.one * shellScale;
                }
                if (swordWave != null)
                {
                    Vector3 tip = core.GetPosition(core.positionCount - 1);
                    Vector3 previous = core.GetPosition(Mathf.Max(0, core.positionCount - 2));
                    Vector3 direction = tip - previous;
                    swordWave.transform.position = tip;
                    swordWave.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 20f);
                    swordWave.transform.localScale = new Vector3(0.72f, 0.38f, 1f) * (0.9f + Mathf.Sin(t * Mathf.PI) * 0.14f);
                    swordWave.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t * 4f));
                }
                yield return null;
            }

            Vector3 impactPoint = GetBeamTarget(target);
            var impact = CreateFxRenderer(effectRoot.transform, "ImpactBurst", 2, 0, 0.10f, 95);
            var shockwave = CreateFxRenderer(effectRoot.transform, "Shockwave", 0, 1, 0.08f, 82);
            var hitSlash = CreateFxRenderer(effectRoot.transform, "HitSwordCrescent", 1, 1, 0.10f, 98);
            if (impact != null) impact.transform.position = impactPoint;
            if (shockwave != null) shockwave.transform.position = impactPoint;
            if (hitSlash != null)
            {
                hitSlash.transform.position = impactPoint;
                hitSlash.transform.rotation = Quaternion.Euler(0f, 0f, curveSide * 28f);
            }
            elapsed = 0f;
            fadeDuration = Mathf.Clamp(fadeDuration, 0f, 0.14f);
            while (elapsed < fadeDuration)
            {
                elapsed += PresentationDelta;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                Vector3 origin = GetBeamOrigin(attacker, target);
                Vector3 destination = GetBeamTarget(target);
                SetBeamCurvePositions(glow, core, origin, destination, 1f, firstBend, secondBend);
                SetBeamAlpha(glow, 0.42f * (1f - t));
                SetBeamAlpha(core, 1f - t);
                SetBeamAlpha(echoLeft, 0.38f * (1f - t));
                SetBeamAlpha(echoRight, secondaryTint.a * (1f - t));
                ScrollTrailTexture(glow, core, echoLeft, echoRight);
                if (projectile != null) projectile.color = new Color(1f, 1f, 1f, 1f - t);
                if (swordWave != null) swordWave.color = new Color(1f, 1f, 1f, 1f - t);
                AnimateImpactSprite(impact, t, 0.18f, 0.65f, 1f - t);
                AnimateImpactSprite(shockwave, t, 0.12f, 1.05f, (1f - t) * 0.65f);
                AnimateImpactSprite(hitSlash, t, 0.25f, 0.85f, 1f - t);
                yield return null;
            }

            DestroyTransientEffect(effectRoot);
        }

        private SpriteRenderer CreateFxRenderer(Transform parent, string name, int column, int rowFromTop,
            float initialScale, int sortingOrder)
        {
            string inkId = column == 0 ? "fx-ring" : column == 1 ? "fx-slash" : "fx-impact";
            var inkSprite = GetInkFxSprite(inkId, 100 + column);
            if (inkSprite != null)
            {
                var ink = new GameObject(name).AddComponent<SpriteRenderer>();
                ink.transform.SetParent(parent, false);
                ink.transform.localScale = Vector3.one * initialScale;
                ink.sprite = inkSprite;
                ink.sortingOrder = sortingOrder;
                return ink;
            }
            if (_skillFxAtlas == null)
            {
                _skillFxAtlas = Resources.Load<Texture2D>("UI/Battle/Battle_Skill_Fx");
                if (_skillFxAtlas == null)
                {
                    Debug.LogError("[BattleView] 技能特效图集加载失败: Resources/UI/Battle/Battle_Skill_Fx");
                    return null;
                }
            }
            int key = rowFromTop * 3 + column;
            if (!_skillFxSprites.TryGetValue(key, out var sprite) || sprite == null)
            {
                float cellWidth = _skillFxAtlas.width / 3f;
                float cellHeight = _skillFxAtlas.height / 2f;
                float y = rowFromTop == 0 ? cellHeight : 0f;
                sprite = Sprite.Create(_skillFxAtlas,
                    new Rect(column * cellWidth, y, cellWidth, cellHeight),
                    new Vector2(0.5f, 0.5f), cellHeight);
                sprite.name = $"BattleSkillFx-{column}-{rowFromTop}";
                _skillFxSprites[key] = sprite;
            }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * initialScale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void AnimateImpactSprite(SpriteRenderer renderer, float t,
            float startScale, float endScale, float alpha)
        {
            if (renderer == null) return;
            float scale = Mathf.Lerp(startScale, endScale, Mathf.SmoothStep(0f, 1f, t));
            renderer.transform.localScale = Vector3.one * scale;
            renderer.transform.Rotate(0f, 0f, 150f * PresentationDelta);
            renderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        }
        private static Vector3 GetBeamOrigin(UnitVisual attacker, UnitVisual target)
        {
            Vector3 targetPoint = GetBeamTarget(target);
            if (attacker?.Renderer == null)
            {
                var fallback = attacker?.Sphere != null ? attacker.Sphere.position : Vector3.zero;
                fallback.z = -0.5f;
                return fallback;
            }

            var bounds = attacker.Renderer.bounds;
            Vector3 direction = (targetPoint - bounds.center).normalized;
            var origin = bounds.center + Vector3.up * (bounds.extents.y * 0.22f) +
                         direction * (bounds.extents.x * 0.65f);
            origin.z = -0.5f;
            return origin;
        }

        private static Vector3 GetBeamTarget(UnitVisual target)
        {
            Vector3 destination;
            if (target?.Renderer != null)
            {
                destination = target.Renderer.bounds.center;
            }
            else
            {
                destination = target?.Sphere != null ? target.Sphere.position : Vector3.zero;
            }
            destination.z = -0.5f;
            return destination;
        }

        private LineRenderer CreateBeamLine(Transform parent, string name, float width, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = width;
            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.04f),
                new Keyframe(0.12f, 0.42f),
                new Keyframe(0.38f, 1f),
                new Keyframe(0.78f, 0.58f),
                new Keyframe(1f, 0.08f));
            line.numCapVertices = 12;
            line.numCornerVertices = 10;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Tile;
            line.textureScale = new Vector2(2.8f, 1f);
            line.sortingOrder = name.Contains("Core") ? 64 : 56;
            line.sharedMaterial = GetOrCreateBeamMaterial();

            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.Lerp(color, Color.white, 0.2f), 0f),
                    new GradientColorKey(color, 0.42f),
                    new GradientColorKey(Color.Lerp(color, Color.white, 0.72f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(color.a * 0.72f, 0.14f),
                    new GradientAlphaKey(color.a, 0.7f),
                    new GradientAlphaKey(color.a * 0.18f, 1f),
                });
            line.colorGradient = gradient;
            return line;
        }

        private Material GetOrCreateBeamMaterial()
        {
            if (_beamMaterial != null) return _beamMaterial;
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            _beamMaterial = new Material(shader) { name = "BattleEnergyRibbon", renderQueue = 3100 };
            _beamMaterial.mainTexture = GetOrCreateBeamTrailTexture();
            SetMaterialColor(_beamMaterial, Color.white);
            return _beamMaterial;
        }

        private Texture2D GetOrCreateBeamTrailTexture()
        {
            if (_beamTrailTexture != null) return _beamTrailTexture;
            const int width = 128;
            const int height = 16;
            _beamTrailTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "RuntimeEnergyRibbon",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float vertical = 1f - Mathf.Abs((y + 0.5f) / height * 2f - 1f);
                vertical = Mathf.Pow(Mathf.Clamp01(vertical), 1.7f);
                for (int x = 0; x < width; x++)
                {
                    float along = (x + 0.5f) / width;
                    float bands = 0.58f + 0.24f * Mathf.Sin(along * Mathf.PI * 12f) +
                                  0.18f * Mathf.Sin(along * Mathf.PI * 29f + y * 0.7f);
                    float spark = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(along * Mathf.PI * 7f)), 10f) * 0.5f;
                    float alpha = Mathf.Clamp01(vertical * (bands + spark));
                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            _beamTrailTexture.SetPixels(pixels);
            _beamTrailTexture.Apply(false, true);
            return _beamTrailTexture;
        }

        private void ScrollTrailTexture(params LineRenderer[] lines)
        {
            float offset = -PresentationTime * 2.6f;
            foreach (var line in lines)
            {
                if (line != null && line.sharedMaterial != null)
                {
                    line.sharedMaterial.mainTextureOffset = new Vector2(offset, 0f);
                }
            }
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private void SetBeamCurvePositions(LineRenderer glow, LineRenderer core,
            Vector3 origin, Vector3 destination, float progress, float firstBend, float secondBend)
        {
            SetMovingRibbon(glow, origin, destination, progress, firstBend, secondBend, 0.34f, 0f);
            SetMovingRibbon(core, origin, destination, progress, firstBend, secondBend, 0.27f, 0f);
        }

        private void SetSingleTrail(LineRenderer line, Vector3 origin, Vector3 destination,
            float progress, float firstBend, float secondBend)
        {
            SetMovingRibbon(line, origin, destination, progress, firstBend, secondBend, 0.24f, 0.014f);
        }

        private void SetMovingRibbon(LineRenderer line, Vector3 origin, Vector3 destination,
            float progress, float firstBend, float secondBend, float tailRatio, float shimmerStrength)
        {
            progress = Mathf.Clamp01(progress);
            float startProgress = Mathf.Max(0f, progress - tailRatio);
            float visibleSpan = Mathf.Max(0.01f, progress - startProgress);
            int segments = Mathf.Clamp(Mathf.CeilToInt(visibleSpan * 54f), 3, 22);
            line.positionCount = segments + 1;

            Vector3 delta = destination - origin;
            float distance = delta.magnitude;
            Vector3 side = distance > Mathf.Epsilon
                ? new Vector3(-delta.y, delta.x, 0f).normalized
                : Vector3.up;
            Vector3 control1 = origin + delta * 0.34f + side * (distance * firstBend);
            Vector3 control2 = origin + delta * 0.7f + side * (distance * secondBend);

            for (int i = 0; i <= segments; i++)
            {
                float local = (float)i / segments;
                float t = Mathf.Lerp(startProgress, progress, local);
                float one = 1f - t;
                Vector3 point = one * one * one * origin +
                                3f * one * one * t * control1 +
                                3f * one * t * t * control2 +
                                t * t * t * destination;
                if (shimmerStrength > 0f)
                {
                    float envelope = Mathf.Sin(local * Mathf.PI);
                    float shimmer = Mathf.Sin((t * 13f - PresentationTime * 7f) * Mathf.PI) *
                                    distance * shimmerStrength * envelope;
                    point += side * shimmer;
                }
                line.SetPosition(i, point);
            }
        }
        private static void SetBeamAlpha(LineRenderer line, float alpha)
        {
            var start = line.startColor;
            var end = line.endColor;
            start.a = alpha;
            end.a = alpha;
            line.startColor = start;
            line.endColor = end;
        }

        private void DestroyTransientEffect(GameObject effect)
        {
            if (effect == null)
            {
                return;
            }
            _transientEffects.Remove(effect);
            // 光带通过顶点颜色区分技能，共享材质由 BattleView 销毁时统一释放。
            Destroy(effect);
        }

        private void ClearTransientEffects()
        {
            foreach (var effect in new List<GameObject>(_transientEffects))
            {
                DestroyTransientEffect(effect);
            }
            _transientEffects.Clear();
        }

        private IEnumerator FlashHit(UnitVisual visual)
        {
            if (visual == null || visual.Body == null || visual.ReactionRoot == null || visual.DeathStarted)
            {
                yield break;
            }

            SetRendererColor(visual.Renderer, Color.Lerp(Color.white, Q.Cinnabar, .22f));
            float elapsed = 0f;
            const float duration = .22f;
            while (elapsed < duration && !visual.DeathStarted)
            {
                elapsed += PresentationDelta;
                float t = Mathf.Clamp01(elapsed / duration);
                // 受击层独立于施法层，短促后移后恢复，整张插画始终朝向相机。
                float strength = t < .25f ? Mathf.SmoothStep(0f, 1f, t / .25f)
                    : 1f - Mathf.SmoothStep(0f, 1f, (t - .25f) / .75f);
                visual.ReactionRoot.localPosition = new Vector3(0f,
                    (visual.IsDefender ? .065f : -.065f) * strength, 0f);
                visual.ReactionRoot.localRotation = Quaternion.Euler(0f, 0f,
                    (visual.IsDefender ? -5f : 5f) * strength);
                yield return null;
            }
            visual.ReactionRoot.localPosition = Vector3.zero;
            visual.ReactionRoot.localRotation = Quaternion.identity;
            if (!visual.DeathStarted) SetRendererColor(visual.Renderer, visual.BaseColor);
        }

        private IEnumerator AnimateFloatingNumber(UnitVisual visual, long amount, bool healing)
        {
            if (!CanShowTransientFeedback || visual?.Sphere == null || amount <= 0) yield break;
            var go = new GameObject(healing ? "HealingNumber" : "DamageNumber");
            go.transform.SetParent(_worldRoot.transform, false);
            _transientEffects.Add(go);
            var label = go.AddComponent<TextMeshPro>();
            label.text = healing ? $"+{amount:N0}" : $"-{amount:N0}";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 2.25f;
            label.fontStyle = FontStyles.Bold;
            label.color = healing ? Q.Jade : Q.Cinnabar;
            label.outlineColor = Q.Paper;
            label.outlineWidth = .18f;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            if (_uiFont != null) label.font = _uiFont;
            label.sortingOrder = 120;
            Vector3 start = GetBeamTarget(visual) + new Vector3(0f, 0.32f, -0.25f);
            const float duration = 0.48f;
            float elapsed = 0f;
            while (elapsed < duration && CanShowTransientFeedback)
            {
                elapsed += PresentationDelta;
                float t = Mathf.Clamp01(elapsed / duration);
                go.transform.position = start + Vector3.up * Mathf.Lerp(0f, 0.38f, Mathf.SmoothStep(0f, 1f, t));
                float maxHp = _unitMaxHp.TryGetValue(visual.UnitId, out var knownMax) && knownMax > 0
                    ? knownMax
                    : amount;
                float emphasis = Mathf.Clamp01((float)((double)amount / maxHp));
                float numberScale = 1f + emphasis * 0.28f;
                float pop = Mathf.Sin(Mathf.Clamp01(t * 3.5f) * Mathf.PI) * 0.08f;
                go.transform.localScale = Vector3.one * Mathf.Lerp(0.72f, numberScale + pop, Mathf.Clamp01(t * 5f));
                var color = label.color;
                color.a = 1f - Mathf.SmoothStep(0.68f, 1f, t);
                label.color = color;
                yield return null;
            }
            DestroyTransientEffect(go);
        }

        private IEnumerator AnimateCombatPulse(UnitVisual visual, Color tint, string caption)
        {
            if (!CanShowTransientFeedback || visual?.Sphere == null)
            {
                yield break;
            }

            var effect = new GameObject("CombatFeedback");
            effect.transform.SetParent(_worldRoot.transform, false);
            _transientEffects.Add(effect);
            var ring = caption == "眩晕"
                ? CreateControlFx(effect.transform, "StunImpactRing", true, .1f, 94)
                : CreateFxRenderer(effect.transform, "FeedbackRing", 0, 1, 0.1f, 94);
            var label = string.IsNullOrEmpty(caption) ? null : effect.AddComponent<TextMeshPro>();
            if (label != null)
            {
                label.text = caption;
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = 1.45f;
                label.fontStyle = FontStyles.Bold;
                label.color = tint;
                label.outlineColor = new Color(0.015f, 0.055f, 0.075f, 0.9f);
                label.outlineWidth = 0.18f;
                label.enableWordWrapping = false;
                label.overflowMode = TextOverflowModes.Overflow;
                if (_uiFont != null) label.font = _uiFont;
                label.sortingOrder = 122;
            }

            const float duration = 0.34f;
            float elapsed = 0f;
            while (elapsed < duration && CanShowTransientFeedback)
            {
                elapsed += PresentationDelta;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector3 target = GetBeamTarget(visual);
                target.z = -0.62f;
                effect.transform.position = target;
                if (ring != null)
                {
                    ring.transform.localPosition = Vector3.zero;
                    AnimateImpactSprite(ring, t, 0.1f, 0.46f, 0.72f * (1f - t));
                    ring.color = new Color(tint.r, tint.g, tint.b, 0.72f * (1f - t));
                }
                if (label != null)
                {
                    label.transform.localPosition = Vector3.up * (0.32f + t * 0.34f);
                    var color = tint;
                    color.a = 1f - Mathf.SmoothStep(0.55f, 1f, t);
                    label.color = color;
                    label.transform.localScale = Vector3.one * Mathf.Lerp(0.82f, 1f, Mathf.Clamp01(t * 4f));
                }
                yield return null;
            }
            DestroyTransientEffect(effect);
        }


        private void UpdateHealth(string unitId, long hp, long knownMax)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                return;
            }

            var visual = EnsureUnitVisual(unitId);
            _unitHp[unitId] = System.Math.Max(0L, hp);
            long previousMax = _unitMaxHp.TryGetValue(unitId, out var value) ? value : 0;
            _unitMaxHp[unitId] = System.Math.Max(previousMax, System.Math.Max(hp, knownMax));
            UpdateHealthBar(visual);
            // 致死时当帧停攻，不等受击协程收尾；正血快照也须同步修复旧的死亡显示状态。
            if (hp <= 0 || visual.DeathStarted) ApplyUnitLifeVisual(visual);
            RefreshTeamHealthHud();
        }

        private void CreateHealthBar(UnitVisual visual)
        {
            if (visual?.Sphere == null)
            {
                return;
            }

            var root = new GameObject("HealthBar").transform;
            root.SetParent(visual.Sphere, false);
            float verticalOffset = visual.IsDefender
                ? DefenderHealthBarVerticalOffset
                : AttackerHealthBarVerticalOffset;
            root.localPosition = new Vector3(0f, verticalOffset, -0.55f);
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;

            Color frameColor = visual.IsDefender
                ? new Color(0.48f, 0.075f, 0.045f, 1f)
                : new Color(0.035f, 0.34f, 0.23f, 1f);
            CreateHealthBarLayer(
                root, "Frame", new Vector2(HealthBarWidth + 0.075f, HealthBarHeight + 0.055f),
                Vector3.zero, frameColor, 29);
            CreateHealthBarLayer(
                root, "Track", new Vector2(HealthBarWidth + 0.025f, HealthBarHeight + 0.018f),
                new Vector3(0f, 0f, -0.01f), new Color(0.035f, 0.045f, 0.055f, 0.98f), 30);

            // 拖尾层夹在 Track 与 Fill 之间：Fill 盖住它左半段，露出来的右半段
            // 就是刚刚掉掉的那截血。排序靠后(数字大)的盖在上面。
            var ghostRenderer = CreateHealthBarLayer(
                root, "Ghost", new Vector2(HealthBarWidth, HealthBarHeight),
                new Vector3(0f, 0f, -0.015f), new Color(1f, 0.93f, 0.86f, 0.55f), 31);
            var fillRenderer = CreateHealthBarLayer(
                root, "Fill", new Vector2(HealthBarWidth, HealthBarHeight),
                new Vector3(0f, 0f, -0.02f), Color.white, 32);
            var highlightRenderer = CreateHealthBarLayer(
                root, "Highlight", new Vector2(HealthBarWidth, HealthBarHeight * 0.28f),
                new Vector3(0f, HealthBarHeight * 0.23f, -0.03f),
                new Color(1f, 1f, 1f, 0.34f), 33);

            visual.HealthBarRoot = root;
            visual.HealthBarFill = fillRenderer.transform;
            visual.HealthBarFillRenderer = fillRenderer;
            visual.HealthBarGhost = ghostRenderer.transform;
            visual.HealthBarGhostRenderer = ghostRenderer;
            visual.HealthBarHighlight = highlightRenderer.transform;
            visual.HealthBarHighlightRenderer = highlightRenderer;
            UpdateHealthBar(visual);
            // 首次出场直接就位，不要从空血条涨上来。
            visual.HealthBarFillRatio = visual.HealthBarTargetRatio;
            visual.HealthBarGhostRatio = visual.HealthBarTargetRatio;
            ApplyHealthBarWidths(visual);
        }

        // 生成一条圆角胶囊贴图：左右两端是抗锯齿圆帽，中段可横向拉伸。
        // 九宫格只切左右两边、上下不切，所以拉伸时圆帽始终是正圆而不会被压扁。
        private Sprite GetRoundedBarSprite(float worldHeight)
        {
            int key = Mathf.Max(1, Mathf.RoundToInt(worldHeight * 1000f));
            if (_roundedBarSprites.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            const int radius = 16;
            const int height = radius * 2;
            const int width = height * 2;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"BattleHealthBarRounded-{key}",
            };

            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float dy = Mathf.Abs(y + 0.5f - height * 0.5f);
                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f;
                    // 到胶囊中轴线（y=height/2 上的一段线段）的距离，超出半径即在形外。
                    float dx = Mathf.Max(radius - px, px - (width - radius), 0f);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    pixels[y * width + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, false);

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                height / Mathf.Max(0.0001f, worldHeight),
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, 0f, radius, 0f));
            sprite.name = texture.name;
            _roundedBarSprites[key] = sprite;
            return sprite;
        }

        private SpriteRenderer CreateHealthBarLayer(
            Transform parent,
            string layerName,
            Vector2 size,
            Vector3 localPosition,
            Color color,
            int sortingOrder)
        {
            var layer = new GameObject(layerName);
            layer.transform.SetParent(parent, false);
            layer.transform.localPosition = localPosition;
            layer.transform.localScale = Vector3.one;
            var renderer = layer.AddComponent<SpriteRenderer>();
            // 九宫格拉伸：宽度变化由 renderer.size 驱动，不再靠 transform 缩放，
            // 否则圆帽会跟着横向压扁。
            renderer.sprite = GetRoundedBarSprite(size.y);
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void UpdateHealthBar(UnitVisual visual)
        {
            if (visual?.HealthBarFill == null)
            {
                return;
            }

            float ratio = 1f;
            if (_unitHp.TryGetValue(visual.UnitId, out var hp) &&
                _unitMaxHp.TryGetValue(visual.UnitId, out var maxHp) && maxHp > 0)
            {
                ratio = Mathf.Clamp01((float)((double)hp / maxHp));
            }

            // 掉血才动拖尾：让它停在掉血前的位置，并重新开始停留计时。
            // 之后宽度由 AnimateHealthBars 每帧推进，这里只更新目标值。
            if (ratio < visual.HealthBarTargetRatio - 0.0001f)
            {
                visual.HealthBarGhostRatio = Mathf.Max(
                    visual.HealthBarGhostRatio, visual.HealthBarTargetRatio);
                visual.HealthBarDrainHold = HealthBarDrainHoldSeconds;
            }
            visual.HealthBarTargetRatio = ratio;

            Color lowHealthColor = new Color(0.95f, 0.10f, 0.045f, 1f);
            Color fullHealthColor = visual.IsDefender
                ? new Color(0.96f, 0.22f, 0.10f, 1f)
                : new Color(0.10f, 0.90f, 0.38f, 1f);
            float colorProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(ratio / 0.6f));
            Color fillColor = Color.Lerp(lowHealthColor, fullHealthColor, colorProgress);
            if (visual.HealthBarFillRenderer != null)
            {
                visual.HealthBarFillRenderer.color = fillColor;
            }
            if (visual.HealthBarHighlightRenderer != null)
            {
                visual.HealthBarHighlightRenderer.color = Color.Lerp(
                    fillColor, Color.white, 0.42f) * new Color(1f, 1f, 1f, 0.52f);
            }
            ApplyHealthBarWidths(visual);
        }

        // 每帧推进血条动画：填充快速滚向当前血量，拖尾停留一会儿再慢慢退掉，
        // 两者的差值就是那截正在流失的白色拖尾。
        private void AnimateHealthBars()
        {
            float delta = PresentationDelta;
            if (delta <= 0f)
            {
                return;
            }
            foreach (var visual in _unitVisuals.Values)
            {
                if (visual?.HealthBarRoot == null)
                {
                    continue;
                }
                visual.HealthBarFillRatio = Mathf.MoveTowards(
                    visual.HealthBarFillRatio, visual.HealthBarTargetRatio,
                    delta * HealthBarFillSpeed);
                if (visual.HealthBarGhostRatio > visual.HealthBarFillRatio)
                {
                    visual.HealthBarDrainHold -= delta;
                    if (visual.HealthBarDrainHold <= 0f)
                    {
                        // 拖尾只会退到当前血量为止，并且永远不低于填充条。
                        visual.HealthBarGhostRatio = Mathf.Max(
                            visual.HealthBarFillRatio,
                            Mathf.MoveTowards(visual.HealthBarGhostRatio,
                                visual.HealthBarTargetRatio, delta * HealthBarDrainSpeed));
                    }
                }
                else
                {
                    // 回血：拖尾直接跟上，不留反向空隙。
                    visual.HealthBarGhostRatio = visual.HealthBarFillRatio;
                    visual.HealthBarDrainHold = HealthBarDrainHoldSeconds;
                }
                ApplyHealthBarWidths(visual);
            }
        }

        // 按当前动画比例摆放 Fill / Ghost / Highlight。三者都是左锚定的:
        // 宽度收缩时左边缘保持不动，看起来就是血从右侧退去。
        private static void ApplyHealthBarWidths(UnitVisual visual)
        {
            if (visual?.HealthBarFillRenderer == null)
            {
                return;
            }
            SetHealthBarLayerRatio(visual.HealthBarFill, visual.HealthBarFillRenderer,
                visual.HealthBarFillRatio, HealthBarHeight);
            if (visual.HealthBarGhostRenderer != null)
            {
                SetHealthBarLayerRatio(visual.HealthBarGhost, visual.HealthBarGhostRenderer,
                    visual.HealthBarGhostRatio, HealthBarHeight);
            }
            if (visual.HealthBarHighlightRenderer != null)
            {
                SetHealthBarLayerRatio(visual.HealthBarHighlight,
                    visual.HealthBarHighlightRenderer,
                    visual.HealthBarFillRatio, HealthBarHeight * 0.28f);
            }
        }

        private static void SetHealthBarLayerRatio(Transform layer, SpriteRenderer renderer,
            float ratio, float height)
        {
            if (layer == null || renderer == null)
            {
                return;
            }
            float width = HealthBarWidth * Mathf.Clamp01(ratio);
            renderer.size = new Vector2(width, height);
            var position = layer.localPosition;
            layer.localPosition = new Vector3(
                (width - HealthBarWidth) * 0.5f, position.y, position.z);
        }
        private void ApplyUnitLifeVisual(UnitVisual visual)
        {
            if (visual == null || visual.Sphere == null)
            {
                return;
            }

            bool alive = !_unitHp.TryGetValue(visual.UnitId, out var hp) || hp > 0;
            if (!alive)
            {
                UpdateHealthBar(visual);
                if (!visual.DeathStarted)
                {
                    visual.DeathStarted = true;
                    ClearUnitControlVisual(visual);
                    StopCharacterAttack(visual);
                    if (_reactionQueues.TryGetValue(visual.UnitId, out var pendingReactions))
                    {
                        pendingReactions.Clear();
                    }
                    visual.DeathRoutine = StartCoroutine(AnimateUnitDeath(visual));
                }
                return;
            }

            if (visual.DeathRoutine != null)
            {
                StopCoroutine(visual.DeathRoutine);
                visual.DeathRoutine = null;
            }
            visual.DeathStarted = false;
            visual.Sphere.gameObject.SetActive(true);
            if (visual.Body != null)
            {
                visual.Body.gameObject.SetActive(true);
            }
            UpdateHealthBar(visual);
            if (visual.HealthBarRoot != null)
            {
                visual.HealthBarRoot.localScale = Vector3.one;
            }
            if (visual.CultivationSeal != null)
            {
                visual.CultivationSeal.enabled = true;
            }
            visual.Body.localPosition = visual.BodyBaseLocalPosition;
            visual.Body.localRotation = visual.BodyBaseLocalRotation;
            visual.Body.localScale = visual.BodyBaseLocalScale;
            if (!visual.DeathStarted) SetRendererColor(visual.Renderer, visual.BaseColor);
        }

        private IEnumerator AnimateUnitDeath(UnitVisual visual)
        {
            if (visual?.Sphere == null || visual.Body == null)
            {
                yield break;
            }

            // 失去飞行支撑后缓缓下落、淡出；不把插画缩成点或沿Y轴翻到侧面。
            float fallDirection = (visual.UnitId.GetHashCode() & 1) == 0 ? -1f : 1f;
            Quaternion standingRotation = visual.BodyBaseLocalRotation;
            Vector3 standingPosition = visual.BodyBaseLocalPosition;
            Vector3 standingScale = visual.BodyBaseLocalScale;
            float elapsed = 0f;
            while (elapsed < DeathFallDuration)
            {
                elapsed += PresentationDelta;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / DeathFallDuration));
                visual.Body.localRotation = standingRotation * Quaternion.Euler(0f, 0f, fallDirection * 16f * t);
                visual.Body.localPosition = standingPosition + new Vector3(fallDirection * .08f * t, -.18f * t * t, 0f);
                visual.Body.localScale = standingScale * Mathf.Lerp(1f, .96f, t);
                SetRendererColor(visual.Renderer, new Color(1f, 1f, 1f, Mathf.Lerp(1f, .82f, t)));
                if (visual.HealthBarRoot != null)
                {
                    visual.HealthBarRoot.localScale = Vector3.one * Mathf.Lerp(1f, 0.72f, t);
                }
                if (visual.CultivationSeal != null)
                {
                    var aura = visual.CultivationSeal.color;
                    aura.a = Mathf.Lerp(aura.a, 0.04f, t);
                    visual.CultivationSeal.color = aura;
                }
                yield return null;
            }

            yield return PresentationWait(0.22f);
            elapsed = 0f;
            while (elapsed < DeathFadeDuration)
            {
                elapsed += PresentationDelta;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / DeathFadeDuration));
                visual.Body.localPosition = standingPosition + new Vector3(fallDirection * (.08f + .12f * t), -.18f - .45f * t, 0f);
                visual.Body.localScale = standingScale * Mathf.Lerp(.96f, .88f, t);
                SetRendererColor(visual.Renderer, new Color(1f, 1f, 1f, .82f * (1f - t)));
                if (visual.HealthBarRoot != null)
                {
                    visual.HealthBarRoot.localScale = Vector3.one * Mathf.Lerp(0.72f, 0f, t);
                }
                if (visual.CultivationSeal != null)
                {
                    var aura = visual.CultivationSeal.color;
                    aura.a = Mathf.Lerp(aura.a, 0f, t);
                    visual.CultivationSeal.color = aura;
                }
                yield return null;
            }

            visual.DeathRoutine = null;
            if (visual.Sphere != null)
            {
                visual.Sphere.gameObject.SetActive(false);
            }
        }

        private void ApplyFinished(global::BattleFinishedNtf message)
        {
            _battleIsFinished = true;
            foreach (var unit in message.Units)
            {
                bool isDefender = unit.Team == global::BattleTeam.Defender;
                var visual = EnsureUnitVisual(unit.InstanceId, isDefender);
                _unitHp[unit.InstanceId] = System.Math.Max(0L, unit.Hp);
                _unitMaxHp[unit.InstanceId] = System.Math.Max(0L, unit.MaxHp);
                ApplyUnitLifeVisual(visual);
            }
            RefreshTeamHealthHud();
            _canBeginBattle = true;
            string result = message.Outcome switch
            {
                global::BattleOutcome.AttackerWin => "胜利 · 仙途再进",
                global::BattleOutcome.DefenderWin => "惜败 · 重整旗鼓",
                global::BattleOutcome.Draw => "势均力敌",
                _ => "战斗结束",
            };
            SetBattleStatus(result, "结算完成");
            if (_resultPage != null) Destroy(_resultPage);
            _resultPage = BattleResultPage.Show(transform, message, BeginBattle, ReturnToLobby);
        }
        private void EnsureEventUnits(global::BattleEvent battleEvent)
        {
            if (battleEvent == null)
            {
                return;
            }
            EnsureUnitVisual(battleEvent.ActorId);
            EnsureUnitVisual(battleEvent.TargetId);
            foreach (var targetId in battleEvent.TargetIds)
            {
                EnsureUnitVisual(targetId);
            }
        }

        private UnitVisual EnsureUnitVisual(string unitId, bool? defenderOverride = null)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                return null;
            }

            if (_unitVisuals.TryGetValue(unitId, out var existing))
            {
                if (defenderOverride.HasValue && existing.IsDefender != defenderOverride.Value)
                {
                    (existing.IsDefender ? _defenders : _attackers).Remove(existing);
                    existing.IsDefender = defenderOverride.Value;
                    (existing.IsDefender ? _defenders : _attackers).Add(existing);
                    if (existing.HealthBarRoot != null)
                    {
                        float verticalOffset = existing.IsDefender
                            ? DefenderHealthBarVerticalOffset
                            : AttackerHealthBarVerticalOffset;
                        existing.HealthBarRoot.localPosition = new Vector3(0f, verticalOffset, -0.55f);
                    }
                    UpdateInkCharacterSprite(existing);
                    UpdateHealthBar(existing);
                    LayoutUnitSpheres();
                }
                return existing;
            }

            bool isDefender = defenderOverride ?? IsDefender(unitId);
            var color = Color.white;
            var visual = CreateInkCharacterVisual(unitId, isDefender);
            visual.UnitId = unitId;
            visual.IsDefender = isDefender;
            visual.BaseColor = color;
            var control = new GameObject("ControlPoseRoot").transform;
            control.SetParent(visual.Sphere, false);
            visual.ControlRoot = control;
            var reaction = new GameObject("HitReactionRoot").transform;
            reaction.SetParent(control, false);
            var cast = new GameObject("CastPoseRoot").transform;
            cast.SetParent(reaction, false);
            visual.CastRoot = cast;
            visual.Body.SetParent(cast, false);
            visual.ReactionRoot = reaction;
            PrepareUnitControlVisuals(visual);
            CreateHealthBar(visual);
            CreateCultivationSeal(visual);
            _unitVisuals.Add(unitId, visual);
            (isDefender ? _defenders : _attackers).Add(visual);
            LayoutUnitSpheres();
            return visual;
        }

        private void CreateCultivationSeal(UnitVisual visual)
        {
            if (visual?.Sphere == null)
            {
                return;
            }

            // 图集首格是施法阵纹；压成地面灵环，作为角色的持续仙气视觉锚点。
            var seal = CreateFxRenderer(visual.Sphere, "CultivationSeal", 0, 0, 0.55f, 18);
            if (seal == null)
            {
                return;
            }
            seal.transform.localPosition = new Vector3(0f, 0.035f, -0.36f);
            seal.transform.localScale = new Vector3(0.72f, 0.22f, 1f);
            seal.color = visual.IsDefender
                ? new Color(0.76f, 0.18f, 0.42f, 0.32f)
                : new Color(0.28f, 0.82f, 1f, 0.34f);
            visual.CultivationSeal = seal;
            visual.CultivationSealBaseScale = seal.transform.localScale;
        }

        private void ClearUnitVisuals()
        {
            ClearReferenceUnits();
            ClearTransientEffects();
            foreach (var visual in _unitVisuals.Values)
            {
                if (visual?.Sphere == null)
                {
                    continue;
                }
                StopCharacterAttack(visual);
                visual.Sphere.gameObject.SetActive(false);
                Destroy(visual.Sphere.gameObject);
            }
            _unitVisuals.Clear();
            _attackers.Clear();
            _defenders.Clear();
            _unitHp.Clear();
            _unitMaxHp.Clear();
        }

        private void LayoutUnitSpheres()
        {
            LayoutReferenceFormation();
        }

        private void LayoutLegacyUnitSpheres()
        {
            bool portrait = Screen.height > Screen.width;
            float halfHeight = _battleCamera != null ? _battleCamera.orthographicSize : 5f;
            float aspect = _battleCamera != null && _battleCamera.aspect > 0f
                ? _battleCamera.aspect
                : (portrait ? 9f / 16f : 16f / 9f);

            // 按实际背景尺寸映射擂台 UV；等比铺满时兼容视口裁切。
            float viewportWidth = halfHeight * 2f * aspect;
            float viewportHeight = halfHeight * 2f;
            float centerU = portrait ? PortraitArenaCenterU : LandscapeArenaCenterU;
            float centerV = portrait ? PortraitArenaCenterV : LandscapeArenaCenterV;
            float radiusU = portrait ? PortraitArenaRadiusU : LandscapeArenaRadiusU;
            float radiusV = portrait ? PortraitArenaRadiusV : LandscapeArenaRadiusV;

            Vector2 backgroundSize = _battleBackground != null && _battleBackground.sprite != null
                ? (Vector2)_battleBackground.bounds.size : new Vector2(viewportWidth, viewportHeight);
            var arenaCenter = new Vector2((centerU - 0.5f) * backgroundSize.x,
                (centerV - 0.5f) * backgroundSize.y);
            // Keep edge formations visible when a non-native screen ratio crops the artwork.
            var arenaRadius = new Vector2(Mathf.Min(radiusU * backgroundSize.x, viewportWidth * 0.44f),
                Mathf.Min(radiusV * backgroundSize.y, viewportHeight * 0.20f));

            float standRx = arenaRadius.x * ArenaStandRadiusScale;
            float standRy = arenaRadius.y * ArenaStandRadiusScale;

            LayoutTeamOnArena(_attackers, false, arenaCenter, standRx, standRy);
            LayoutTeamOnArena(_defenders, true, arenaCenter, standRx, standRy);
        }

        // 队伍各自缩放，采用缓和的幂次曲线，避免从 1v1 切到多人关卡时人物突然缩成很小。
        private static float ArenaTeamScale(int count)
        {
            return Mathf.Clamp(ArenaSoloScale / Mathf.Pow(Mathf.Max(1, count), 0.22f),
                ArenaMinUnitScale, ArenaMaxUnitScale);
        }

        // 按一名前排核心 + 两翼后排展开。双方镜像站位、朝向战场中央；人数变化时
        // 只增加外侧编队槽，不再把所有单位挤在同一条弧线上。
        private static void LayoutTeamOnArena(List<UnitVisual> units, bool defender,
            Vector2 arenaCenter, float standRx, float standRy)
        {
            int count = units.Count;
            if (count == 0)
            {
                return;
            }

            float side = defender ? 1f : -1f;
            float teamScale = ArenaTeamScale(count);

            for (int i = 0; i < count; i++)
            {
                GetFormationSlot(i, count, out int rank, out float lateral);
                float inwardOffset = Mathf.Min(0.46f + rank * 0.17f, 0.90f);
                var home = new Vector3(
                    arenaCenter.x + side * standRx * inwardOffset,
                    arenaCenter.y + standRy * lateral,
                    0f);

                // 台面是斜俯视的：越靠前(屏幕下方)越大，越靠后越小。
                // 少了这一层，所有单位一样大，就会像是浮在画面上而不是站在台面上。
                float depth01 = Mathf.Clamp01((arenaCenter.y - home.y) / standRy * 0.5f + 0.5f);
                float roleScale = i == 0 ? ArenaLeaderScale : ArenaSupportScale;
                float scaled = Mathf.Clamp(teamScale * roleScale *
                    Mathf.Lerp(ArenaFarScale, ArenaNearScale, depth01),
                    ArenaMinUnitScale, ArenaMaxUnitScale);

                var visual = units[i];
                visual.Home = home;
                visual.BaseScale = Vector3.one * scaled;
                visual.Sphere.position = home;
                visual.Sphere.localScale = visual.BaseScale;
            }
        }

        private static void GetFormationSlot(int index, int count, out int rank, out float lateral)
        {
            if (count == 1)
            {
                rank = 0;
                lateral = 0f;
                return;
            }

            if (count == 2)
            {
                rank = 0;
                lateral = index == 0 ? -0.36f : 0.36f;
                return;
            }

            if (count == 3)
            {
                rank = index == 0 ? 0 : 1;
                lateral = index == 0 ? 0f : (index == 1 ? -0.43f : 0.43f);
                return;
            }

            if (count == 4)
            {
                rank = index < 2 ? 0 : 1;
                int withinRank = index % 2;
                lateral = withinRank == 0 ? -0.24f : 0.24f;
                if (rank == 1) lateral *= 1.85f;
                return;
            }

            if (index == 0)
            {
                rank = 0;
                lateral = 0f;
                return;
            }

            int supportIndex = index - 1;
            rank = 1 + supportIndex / 3;
            int firstInRank = (rank - 1) * 3;
            int rankCount = Mathf.Min(3, count - 1 - firstInRank);
            int column = supportIndex - firstInRank;
            lateral = (column - (rankCount - 1) * 0.5f) * 0.42f;
        }

        private Color UnitColor(string unitId, bool defender)
        {
            uint hash = 2166136261;
            foreach (char character in unitId)
            {
                hash ^= character;
                hash *= 16777619;
            }
            float variant = hash % 5u * 0.022f;
            float hue = defender ? variant : 0.5f + variant;
            return Color.HSVToRGB(hue, 0.82f, 1f);
        }

        private static bool IsDefender(string unitId) =>
            !string.IsNullOrEmpty(unitId) && unitId.StartsWith("monster-", System.StringComparison.Ordinal);

        private void BuildWorld()
        {
            WarmupInkCharacterAssets();
            _worldRoot = new GameObject("BattleWorld");
            var cameraGo = new GameObject("BattleCamera");
            cameraGo.transform.SetParent(_worldRoot.transform, false);
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            _cameraHomePosition = cameraGo.transform.localPosition;
            _battleCamera = cameraGo.AddComponent<Camera>();
            _battleCamera.clearFlags = CameraClearFlags.SolidColor;
            _battleCamera.backgroundColor = Q.Paper;
            _battleCamera.orthographic = true;
            _battleCamera.orthographicSize = 5f;
            _battleCamera.depth = 20f;

            SelectBattleEnvironment(false);
            if (_landscapeBackgroundSprite == null && _portraitBackgroundSprite == null)
            {
                return;
            }

            var backgroundGo = new GameObject("BattleBackground");
            backgroundGo.transform.SetParent(_worldRoot.transform, false);
            backgroundGo.transform.position = new Vector3(0f, 0f, 3f);
            _battleBackground = backgroundGo.AddComponent<SpriteRenderer>();
            _battleBackground.sortingOrder = -100;
            UpdateBackgroundSprite();
            FitBattleBackground();
        }

        private void LateUpdate()
        {
            UpdateBattleDesignLayout();
            bool isPortrait = Screen.height > Screen.width;
            if (_battleCamera != null && _battleBackground != null &&
                (_backgroundScreenWidth != Screen.width || _backgroundScreenHeight != Screen.height ||
                 _backgroundIsPortrait != isPortrait))
            {
                UpdateBackgroundSprite();
                FitBattleBackground();
                LayoutUnitSpheres();
            }

            UpdateBattleHudInsets();
            AnimateHealthBars();
            AnimateCultivationSeals();
            RefreshReferenceViewport();
            UpdateUnitControlVisuals();
            UpdateCharacterAnimations();
            foreach (var unit in _unitVisuals.Values)
            {
                if (unit.Sphere == null || unit.DeathStarted) continue;
                unit.Sphere.rotation = _battleCamera.transform.rotation;
                unit.Sphere.position = Vector3.Lerp(unit.Sphere.position, AirCastPosition(unit),
                    1f - Mathf.Exp(-PresentationDelta * 8f));
            }
            UpdateUnitControlStars();
            RefreshReferenceHealth();
        }

        private void AnimateCultivationSeals()
        {
            float time = PresentationTime;
            foreach (var visual in _unitVisuals.Values)
            {
                if (visual?.CultivationSeal == null)
                {
                    continue;
                }

                bool alive = !_unitHp.TryGetValue(visual.UnitId, out var hp) || hp > 0;
                float phaseOffset = Mathf.Abs(visual.UnitId.GetHashCode() % 628) * 0.01f;
                float phase = time * 1.7f + phaseOffset;
                float pulse = 1f + Mathf.Sin(phase) * 0.045f;
                visual.CultivationSeal.transform.localScale = visual.CultivationSealBaseScale *
                    pulse * (alive ? 1f : 0.72f);
                visual.CultivationSeal.transform.Rotate(0f, 0f,
                    (visual.IsDefender ? -1f : 1f) * 7f * PresentationDelta);
                Color aura = visual.IsDefender
                    ? new Color(0.76f, 0.18f, 0.42f, alive ? 0.32f : 0.10f)
                    : new Color(0.28f, 0.82f, 1f, alive ? 0.34f : 0.10f);
                aura.a *= 0.9f + Mathf.Sin(phase) * 0.1f;
                visual.CultivationSeal.color = aura;
            }
        }

        private void UpdateBackgroundSprite()
        {
            if (_battleBackground == null)
            {
                return;
            }

            _backgroundIsPortrait = Screen.height > Screen.width;
            var selected = _backgroundIsPortrait ? _portraitBackgroundSprite : _landscapeBackgroundSprite;
            selected ??= _backgroundIsPortrait ? _landscapeBackgroundSprite : _portraitBackgroundSprite;
            _battleBackground.sprite = selected;
        }

        private void FitBattleBackground()
        {
            if (_battleCamera == null || _battleBackground == null || _battleBackground.sprite == null ||
                Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            var spriteSize = _battleBackground.sprite.bounds.size;

            // 等比铺满完整视口，保留场景比例；超出屏幕的部分由相机裁切。
            _battleCamera.rect = new Rect(0f, 0f, 1f, 1f);
            _battleCamera.ResetAspect();

            float viewportHeight = _battleCamera.orthographicSize * 2f;
            float viewportWidth = viewportHeight * _battleCamera.aspect;
            float scaleX = viewportWidth / spriteSize.x;
            float scaleY = viewportHeight / spriteSize.y;
            float coverScale = Mathf.Max(scaleX, scaleY);
            _battleBackground.transform.localScale = new Vector3(coverScale, coverScale, 1f);
            _battleBackground.transform.localPosition = new Vector3(0f, 0f, 3f);
            _backgroundScreenWidth = Screen.width;
            _backgroundScreenHeight = Screen.height;
        }

        private void BuildUi()
        {
            if (_referenceDesign != null) return;
            // The single live HUD is built with QuietInkUI (Q) in the presentation partial.
            // Keep all timeline, log, skill and button bindings on that same responsive design.
            BuildReferenceUi();
        }

        private Image CreateTeamHealthMeter(Transform parent, bool anchorRight, Color fillColor)
        {
            var track = new GameObject("HealthTrack", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(parent, false);
            var trackRect = track.GetComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0f, 0f);
            trackRect.anchorMax = new Vector2(1f, 0f);
            trackRect.offsetMin = anchorRight ? new Vector2(16f, 8f) : new Vector2(88f, 8f);
            trackRect.offsetMax = anchorRight ? new Vector2(-88f, 18f) : new Vector2(-16f, 18f);
            track.GetComponent<Image>().color = new Color(0.008f, 0.02f, 0.03f, 0.98f);
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(track.transform, false);
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(1f, 1f);
            fillRect.offsetMax = new Vector2(-1f, -1f);
            var fill = fillGo.GetComponent<Image>();
            fill.color = fillColor;
            fill.sprite = Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),new Vector2(.5f,.5f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = anchorRight ? (int)Image.OriginHorizontal.Right : (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;
            return fill;
        }

        private TMP_Text CreateAnchoredHudText(Transform parent, string name, string value,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            int fontSize, TextAlignmentOptions alignment, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = value;
            label.font = _uiFont;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.extraPadding = true;
            return label;
        }

        private void CreateHudIconAt(Transform parent, string name, Vector2 anchor,
            Vector2 position, Vector2 size, Rect uv)
        {
            var texture = Resources.Load<Texture2D>("UI/Battle/Battle_Hud_Icons");
            if (texture == null) return;
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.GetComponent<RawImage>();
            image.texture = texture;
            image.uvRect = uv;
            image.raycastTarget = false;
        }

private void UpdateBattleHudInsets()
        {
            RectTransform root = transform as RectTransform;
            if (root == null || _battleHeaderRect == null || _battleCommandDockRect == null) return;

            float rootWidth = Mathf.Max(0f, root.rect.width);
            float rootHeight = Mathf.Max(0f, root.rect.height);
            Rect safeArea = Screen.safeArea;
            if (_lastHudScreenWidth == Screen.width && _lastHudScreenHeight == Screen.height &&
                Mathf.Approximately(_lastHudRootWidth, rootWidth) &&
                Mathf.Approximately(_lastHudRootHeight, rootHeight) &&
                _lastHudSafeArea == safeArea) return;

            float safeTop = MobileGuiStandards.GetSafeTopInset(root);
            float safeBottom = MobileGuiStandards.GetSafeBottomInset(root);
            float headerHeight = Mathf.Clamp(rootHeight * 0.095f, 156f, 184f);
            float dockHeight = Mathf.Clamp(rootHeight * 0.078f, 132f, 152f);
            float dockWidth = Mathf.Min(1360f, Mathf.Max(0f, rootWidth - 48f));
            float cardWidth = Mathf.Min(
                Mathf.Clamp(rootWidth * 0.4f, 280f, 760f),
                Mathf.Max(220f, (rootWidth - 276f) * 0.5f));

            _battleHeaderRect.sizeDelta = new Vector2(0f, headerHeight);
            _battleHeaderRect.anchoredPosition = new Vector2(0f, -safeTop);
            _battleCommandDockRect.sizeDelta = new Vector2(dockWidth, dockHeight);
            _battleCommandDockRect.anchoredPosition = new Vector2(0f, safeBottom + 16f);

            if (_playerCardRect != null) _playerCardRect.sizeDelta = new Vector2(cardWidth, -24f);
            if (_enemyCardRect != null) _enemyCardRect.sizeDelta = new Vector2(cardWidth, -24f);
            if (_battleActionsRect != null)
            {
                float actionsWidth = Mathf.Clamp(dockWidth * 0.43f, 360f, 476f);
                _battleActionsRect.sizeDelta = new Vector2(actionsWidth, 76f);
                _battleActionsRect.anchoredPosition = new Vector2(-22f, 0f);
            }

            _lastHudScreenWidth = Screen.width;
            _lastHudScreenHeight = Screen.height;
            _lastHudRootWidth = rootWidth;
            _lastHudRootHeight = rootHeight;
            _lastHudSafeArea = safeArea;
        }

        private void RefreshTeamHealthHud()
        {
            RefreshOneTeamHealthHud(false, _playerHealthText, _playerHealthFill);
            RefreshOneTeamHealthHud(true, _enemyHealthText, _enemyHealthFill);
        }

        private void RefreshOneTeamHealthHud(bool defenders, TMP_Text label, Image fill)
        {
            if (label == null || fill == null) return;
            long currentHp = 0L;
            long maximumHp = 0L;
            foreach (var visual in _unitVisuals.Values)
            {
                if (visual == null || visual.IsDefender != defenders) continue;
                if (_unitHp.TryGetValue(visual.UnitId, out long hp))
                    currentHp += System.Math.Max(0L, hp);
                if (_unitMaxHp.TryGetValue(visual.UnitId, out long maxHp))
                    maximumHp += System.Math.Max(0L, maxHp);
            }
            if (maximumHp <= 0L)
            {
                label.text = "生命待命";
                fill.fillAmount = 0f;
                return;
            }
            currentHp = System.Math.Min(currentHp, maximumHp);
            fill.fillAmount = Mathf.Clamp01((float)((double)currentHp / maximumHp));
            label.text = $"生命  {currentHp:N0} / {maximumHp:N0}";
        }

private void CreateHudCard(Transform parent, string name, bool anchorRight, Color color)
        {
            var card = new GameObject(name, typeof(RectTransform), typeof(Image));
            card.transform.SetParent(parent, false);
            var rect = card.GetComponent<RectTransform>();
            rect.anchorMin = anchorRight ? new Vector2(1f, 0f) : Vector2.zero;
            rect.anchorMax = anchorRight ? Vector2.one : new Vector2(0f, 1f);
            rect.pivot = anchorRight ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
            rect.anchoredPosition = anchorRight ? new Vector2(-24f, 0f) : new Vector2(24f, 0f);
            rect.sizeDelta = new Vector2(460f, -24f);
            var cardImage = card.GetComponent<Image>();
            cardImage.color = color;
            cardImage.raycastTarget = false;

            Color edgeColor = anchorRight
                ? new Color(0.95f, 0.4f, 0.27f, 0.88f)
                : new Color(0.31f, 0.79f, 0.9f, 0.88f);
            CreateHudRule(card.transform, "CardTopRail",
                new Vector2(0f, 1f), Vector2.one, Vector2.zero, new Vector2(0f, 1.5f),
                new Color(0.91f, 0.69f, 0.31f, 0.82f));
            CreateHudRule(card.transform, "CardBottomRail",
                Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 1f),
                new Color(0.91f, 0.69f, 0.31f, 0.42f));
            CreateHudRule(card.transform, "TeamEdge",
                anchorRight ? Vector2.zero : new Vector2(1f, 0f),
                anchorRight ? new Vector2(0f, 1f) : Vector2.one,
                Vector2.zero, new Vector2(2f, 0f), edgeColor);

            if (anchorRight) _enemyCardRect = rect;
            else _playerCardRect = rect;

            CreateHudIconAt(card.transform, anchorRight ? "EnemyCrest" : "HeroCrest",
                anchorRight ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f),
                anchorRight ? new Vector2(-42f, 0f) : new Vector2(42f, 0f),
                new Vector2(66f, 66f),
                anchorRight
                    ? new Rect(1f / 3f, 0.5f, 1f / 3f, 0.5f)
                    : new Rect(0f, 0.5f, 1f / 3f, 0.5f));

            Vector2 textMin = anchorRight ? new Vector2(16f, -44f) : new Vector2(90f, -44f);
            Vector2 textMax = anchorRight ? new Vector2(-90f, -20f) : new Vector2(-16f, -20f);
            TextAlignmentOptions alignment = anchorRight
                ? TextAlignmentOptions.MidlineRight
                : TextAlignmentOptions.MidlineLeft;

            CreateAnchoredHudText(card.transform, anchorRight ? "EnemyFaction" : "PlayerFaction",
                anchorRight ? "幻境守关" : "我方主阵",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                textMin, textMax, 16, alignment,
                anchorRight ? new Color(1f, 0.69f, 0.56f) : new Color(0.59f, 0.85f, 0.91f));

            textMin = anchorRight ? new Vector2(16f, -73f) : new Vector2(90f, -73f);
            textMax = anchorRight ? new Vector2(-90f, -46f) : new Vector2(-16f, -46f);
            CreateAnchoredHudText(card.transform, anchorRight ? "EnemyName" : "PlayerName",
                anchorRight ? "幻境守卫 · 魔将" : "清风道人",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                textMin, textMax, 26, alignment,
                anchorRight ? new Color(1f, 0.9f, 0.82f) : new Color(0.91f, 0.97f, 1f));

            textMin = anchorRight ? new Vector2(16f, -99f) : new Vector2(90f, -99f);
            textMax = anchorRight ? new Vector2(-90f, -76f) : new Vector2(-16f, -76f);
            CreateAnchoredHudText(card.transform, anchorRight ? "EnemyRealm" : "PlayerRealm",
                anchorRight ? "试炼之影" : "筑基中期",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                textMin, textMax, 17, alignment,
                anchorRight ? new Color(0.95f, 0.57f, 0.43f) : new Color(0.47f, 0.75f, 0.83f));

            textMin = anchorRight ? new Vector2(16f, -126f) : new Vector2(90f, -126f);
            textMax = anchorRight ? new Vector2(-90f, -102f) : new Vector2(-16f, -102f);
            TMP_Text healthText = CreateAnchoredHudText(card.transform,
                anchorRight ? "EnemyHealth" : "PlayerHealth", "生命待命",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                textMin, textMax, 16, alignment, new Color(0.87f, 0.9f, 0.9f));
            if (anchorRight)
            {
                _enemyHealthText = healthText;
                _enemyHealthFill = CreateTeamHealthMeter(card.transform, true,
                    new Color(1f, 0.39f, 0.26f, 1f));
            }
            else
            {
                _playerHealthText = healthText;
                _playerHealthFill = CreateTeamHealthMeter(card.transform, false,
                    new Color(0.31f, 0.88f, 0.72f, 1f));
            }
        }



        private GameObject CreatePanel(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(transform, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = anchorMax.y > 0.5f ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f); rect.anchoredPosition = position; rect.sizeDelta = size;
            var skin=go.GetComponent<Image>();skin.sprite=ImmortalArtKit.Sprite("panels","jade_dark");skin.type=Image.Type.Sliced;skin.pixelsPerUnitMultiplier=4;skin.color=Color.white; return go;
        }

        private static void CreateAccent(Transform parent, Vector2 anchorMin, Vector2 anchorMax, float height, Color color)
        {
            var go = new GameObject("Accent", typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = anchorMin; rect.sizeDelta = new Vector2(0f, height); go.GetComponent<Image>().color = color;
        }

private static RectTransform CreateHudRule(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }









        private void SetBattleStatus(string status, string round)
        {
            AppendReferenceLog(status);
            if (_roundText != null) _roundText.text = round.StartsWith("时序") || round.StartsWith("战斗序列")
                ? BattleClockLabel((long)(Mathf.Max(0f, PresentationTime - _timelineEpoch) / _tickDurationSeconds))
                : round;
        }

        private string BattleClockLabel(long tick)
        {
            int seconds = Mathf.Max(0, (int)(tick * _tickDurationSeconds));
            return $"{seconds / 60:00}:{seconds % 60:00}";
        }

private Button CreateButton(Transform parent, string name, string label,
            Rect iconUv, bool primary, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            bool anchorRight = !primary;
            rect.anchorMin = anchorRight ? new Vector2(0.5f, 0f) : Vector2.zero;
            rect.anchorMax = anchorRight ? Vector2.one : new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = anchorRight ? new Vector2(10f, 0f) : new Vector2(0f, 0f);
            rect.offsetMax = anchorRight ? new Vector2(0f, 0f) : new Vector2(-10f, 0f);

            var image = go.GetComponent<Image>();
            image.color = primary
                ? new Color(0.12f, 0.43f, 0.5f, 1f)
                : new Color(0.075f, 0.15f, 0.2f, 1f);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            image.sprite=ImmortalArtKit.Sprite("controls",primary?"button_gold":"button_jade");
            image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=4;image.color=Color.white;
            CreateAccent(go.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), 1.5f,
                new Color(1f, 0.75f, 0.31f, primary ? 0.98f : 0.55f));

            CreateHudIconAt(go.transform, "Icon", new Vector2(0f, 0.5f),
                new Vector2(34f, 0f), new Vector2(38f, 38f), iconUv);
            CreateAnchoredHudText(go.transform, "Text", label,
                Vector2.zero, Vector2.one,
                new Vector2(62f, 0f), new Vector2(-8f, 0f), 23,
                TextAlignmentOptions.Center, new Color(1f, 0.93f, 0.76f));
            return button;
        }
    }
}
