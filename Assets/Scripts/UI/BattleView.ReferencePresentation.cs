using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Q = XiuXian.Core.QuietInkUI;

namespace XiuXian.Core
{
    public sealed partial class BattleView
    {
        private RectTransform _referenceDesign;
        private readonly Dictionary<string, ReferenceUnitHud> _referenceUnitHuds = new();
        private readonly Queue<string> _referenceLog = new();
        private readonly List<string> _observedSkills = new();
        private readonly List<TMP_Text> _skillLabels = new();
        private readonly List<RawImage> _skillImages = new();
        private TMP_Text _environmentLabel;
        private float _presentationTime;
        private float PresentationDelta => Time.unscaledDeltaTime;
        private float PresentationTime => _presentationTime;
        private Vector2 _referenceViewport;
        private Vector3 _referenceScale;
        private Vector2 _referencePosition;

        private sealed class ReferenceUnitHud
        {
            public RectTransform Root;
            public TMP_Text Name;
            public TMP_Text Health;
            public RectTransform Fill;
            public TMP_Text Status;
            public RectTransform StatusRoot;
            public RectTransform[] ControlDots;
            public bool StunVisible;
        }

        private void Update() { _presentationTime += PresentationDelta; }

        private IEnumerator PresentationWait(float duration)
        {
            float elapsed = 0;
            while (elapsed < duration) { elapsed += PresentationDelta; yield return null; }
        }

        private void BuildReferenceUi()
        {
            _referenceDesign = Q.Rect(transform, "BattleInkPresentation", 0, 0, 941, 1672);
            _referenceDesign.anchorMin = _referenceDesign.anchorMax = _referenceDesign.pivot = new Vector2(.5f, .5f);
            _referenceDesign.anchoredPosition = Vector2.zero;
            Q.BackButton(_referenceDesign, "ExitBattle", 24, 24, ReturnToLobby);
            Q.Text(_referenceDesign, "StageTitle", "天道演武", 210, 45, 521, 78, 48);
            _roundText = Q.Text(_referenceDesign, "Timeline", "备战", 270, 128, 401, 46, 30);
            _environmentLabel = Q.Text(_referenceDesign, "Environment", "", 270, 178, 401, 36, 24, Q.Muted);
            _environmentLabel.gameObject.SetActive(false);

            var dock = Q.Rect(_referenceDesign, "CommandPaper", 0, 1578, 941, 94).gameObject.AddComponent<Image>();
            dock.color = new Color(Q.Paper.r, Q.Paper.g, Q.Paper.b, .93f);
            dock.raycastTarget = false;
            string[] skills = { "skill_jade", "skill_blue", "skill_wind", "skill_fire" };
            string[] fallback = { "attack_aoe", "healing", "souljade", "flame" };
            for (int i = 0; i < skills.Length; i++)
            {
                int slot = i;
                var button = Q.Button(_referenceDesign, "SkillSlot" + i, "", 67 + i * 211, 1416, 174, 150,
                    () => DescribeSkill(slot), null);
                string artId = Q.Sprite(skills[i]) != null ? skills[i] : fallback[i];
                var art = Q.RawArt(button.transform, artId, 12, 0, 150, 150, true);
                art.name = "SkillArt";
                _skillImages.Add(art);
                // Retain a live textual binding for accessibility/debug inspection; reference art has no captions.
                var label = Q.Text(button.transform, "SkillName", "", 0, 150, 174, 30, 22);
                label.gameObject.SetActive(false);
                _skillLabels.Add(label);
            }

            _battleStatusText = Q.Text(_referenceDesign, "BattleLog", "等待服务器战斗数据", 58, 1588, 825, 72,
                25, Q.Ink, TextAlignmentOptions.TopLeft);
            _battleStatusText.richText = true;
            _battleStatusText.enableWordWrapping = false;
            _battleStatusText.overflowMode = TextOverflowModes.Ellipsis;
            RefreshEnvironmentLabel();
            RememberBattleHudLayout();
            UpdateBattleDesignLayout();
        }

        private static Button ReferenceControl(Transform parent, string name, string label,
            float x, float y, float w, float h, UnityEngine.Events.UnityAction action, int size)
            => Q.Button(parent, name, label, x, y, w, h, action,
                name == "ExitBattle" ? null : "button_secondary", size);

        private void DescribeSkill(int slot)
        {
            AppendReferenceLog(slot < _observedSkills.Count ? "技能由服务端自动释放" : "等待本场技能释放");
        }

        private static string EscapeUi(string value) => (value ?? "").Replace("<", "‹").Replace(">", "›");

        private void AppendReferenceLog(string text)
        {
            _referenceLog.Enqueue(text);
            while (_referenceLog.Count > 2) _referenceLog.Dequeue();
            if (_battleStatusText != null) _battleStatusText.text = string.Join("\n", _referenceLog);
        }

        private string ReferenceUnitName(string id)
        {
            if (id != null && _unitVisuals.TryGetValue(id, out var unit))
            {
                var team = unit.IsDefender ? _defenders : _attackers;
                return (unit.IsDefender ? "敌方" : "我方") + (team.IndexOf(unit) + 1);
            }
            return EscapeUi(id);
        }

        private void RecordReferenceEvent(global::BattleEvent e)
        {
            if (_roundText != null) _roundText.text = BattleClockLabel(e.Tick);
            string timestamp = "[" + BattleClockLabel(e.Tick) + "] ";
            string actor = ReferenceUnitName(e.ActorId), target = ReferenceUnitName(e.TargetId);
            if (e.Type == global::BattleEventType.SkillUsed)
            {
                if (!string.IsNullOrEmpty(e.SkillId) && _unitVisuals.TryGetValue(e.ActorId, out var caster) && !caster.IsDefender)
                {
                    int slot = _observedSkills.IndexOf(e.SkillId);
                    if (slot < 0 && _observedSkills.Count < 4) { slot = _observedSkills.Count; _observedSkills.Add(e.SkillId); }
                    if (slot >= 0 && slot < _skillLabels.Count)
                    {
                        _skillLabels[slot].text = EscapeUi(e.SkillId);
                        for (int i = 0; i < _skillImages.Count; i++)
                            _skillImages[i].color = i == slot ? Color.white : new Color(.88f, .88f, .88f);
                    }
                }
                string scope = e.TargetIds.Count > 1 ? " · " + e.TargetIds.Count + "个目标" : "";
                AppendReferenceLog(timestamp + actor + "施放<color=#347F78>灵诀</color>" + scope);
            }
            else if (e.Type == global::BattleEventType.Damage)
                AppendReferenceLog(timestamp + target + "受到 <color=#BA634D>" + System.Math.Max(0L, e.HpBefore - e.HpAfter) + "</color> 伤害");
            else if (e.Type == global::BattleEventType.Heal)
                AppendReferenceLog(timestamp + target + "恢复 <color=#347F78>" + System.Math.Max(0L, e.HpAfter - e.HpBefore) + "</color> 气血");
        }

        private void ClearReferenceUnits()
        {
            foreach (var hud in _referenceUnitHuds.Values) if (hud.Root != null) Destroy(hud.Root.gameObject);
            _referenceUnitHuds.Clear();
            _referenceLog.Clear();
            _observedSkills.Clear();
            foreach (var text in _skillLabels) text.text = "";
            foreach (var image in _skillImages) image.color = Color.white;
        }

        private void CreateReferenceUnitHud(UnitVisual unit)
        {
            if (_referenceDesign == null || _referenceUnitHuds.ContainsKey(unit.UnitId)) return;
            var root = Q.Rect(_referenceDesign, "UnitHud-" + unit.UnitId, 0, 0, 180, 54);
            var name = Q.Text(root, "UnitName", ReferenceUnitName(unit.UnitId), 0, 0, 180, 25, 20, Q.Ink);
            var frame = Q.Rect(root, "HealthFrame", 3, 28, 174, 15);
            var frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.sprite = GetRoundedBarSprite(.1f);
            frameImage.type = Image.Type.Sliced;
            frameImage.color = Q.Ink;
            frameImage.raycastTarget = false;
            var track = Q.Rect(frame, "Track", 3, 3, 168, 9);
            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = new Color(.87f, .83f, .73f);
            trackImage.raycastTarget = false;
            var fill = Q.Rect(track, "Fill", 0, 0, 168, 9);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = unit.IsDefender ? Q.Cinnabar : Q.Jade;
            fillImage.raycastTarget = false;
            var health = Q.Text(root, "Health", "", -10, 46, 200, 28, 20, Q.Cinnabar);
            health.outlineColor = Q.Paper;
            health.outlineWidth = .16f;
            health.gameObject.SetActive(false);
            var statusRoot = Q.Rect(root, "ControlBadge", 40, -33, 100, 30);
            var back = statusRoot.gameObject.AddComponent<Image>();
            back.sprite = frameImage.sprite;
            back.type = Image.Type.Sliced;
            back.color = new Color(Q.Paper.r, Q.Paper.g, Q.Paper.b, .94f);
            back.raycastTarget = false;
            var status = Q.Text(statusRoot, "ControlStatus", "眩晕", 0, 0, 100, 30, 24, Q.Gold);
            statusRoot.gameObject.SetActive(false);
            var dots = new RectTransform[3];
            for (int i = 0; i < dots.Length; i++)
            {
                dots[i] = Q.Rect(root, "ControlOrbit" + i, 0, 0, 5, 5);
                var dot = dots[i].gameObject.AddComponent<Image>();
                dot.sprite = frameImage.sprite;
                dot.color = Q.Gold;
                dot.raycastTarget = false;
                dots[i].gameObject.SetActive(false);
            }
            if (unit.CultivationSeal != null) unit.CultivationSeal.gameObject.SetActive(false);
            _referenceUnitHuds[unit.UnitId] = new ReferenceUnitHud
            {
                Root = root, Name = name, Health = health, Fill = fill,
                Status = status, StatusRoot = statusRoot, ControlDots = dots
            };
            if (unit.HealthBarRoot != null) unit.HealthBarRoot.gameObject.SetActive(false);
        }

        private void RefreshReferenceHealth()
        {
            foreach (var pair in _referenceUnitHuds)
            {
                if (!_unitVisuals.TryGetValue(pair.Key, out var unit) || unit.Sphere == null) continue;
                var hud = pair.Value;
                hud.Root.gameObject.SetActive(!unit.DeathStarted && unit.Sphere.gameObject.activeInHierarchy);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_referenceDesign,
                    _battleCamera.WorldToScreenPoint(UnitHeadPosition(unit)), null, out var local))
                    hud.Root.anchoredPosition = new Vector2(local.x + _referenceDesign.rect.width * .5f - 90f,
                        local.y - _referenceDesign.rect.height * .5f + 43f);
                bool known = _unitHp.TryGetValue(pair.Key, out long hp);
                _unitMaxHp.TryGetValue(pair.Key, out long max);
                bool lowHealth = known && hp > 0 && max > 0 && (double)hp / max <= .15;
                hud.Health.gameObject.SetActive(lowHealth);
                if (lowHealth) hud.Health.text = $"{hp:N0}/{max:N0}";
                float minimumWidth = Mathf.Max(3f, 2f / Mathf.Max(.1f, Mathf.Abs(_referenceDesign.lossyScale.x)));
                float width = known && hp > 0 ? Mathf.Max(minimumWidth, 168f * unit.HealthBarFillRatio) : 0f;
                hud.Fill.sizeDelta = new Vector2(Mathf.Min(168f, width), 9);
                RefreshUnitControlHud(unit, hud);
            }
        }

        private Vector3 ReferenceWorld(float x, float y)
        {
            Vector3 screen = RectTransformUtility.WorldToScreenPoint(null,
                _referenceDesign.TransformPoint(new Vector3(x - _referenceDesign.rect.width * .5f,
                    _referenceDesign.rect.height * .5f - y, 0)));
            screen.z = 10;
            return _battleCamera.ScreenToWorldPoint(screen);
        }

        private void LayoutReferenceFormation()
        {
            if (_referenceDesign == null || _battleCamera == null) return;
            LayoutReferenceTeam(_defenders, true);
            LayoutReferenceTeam(_attackers, false);
        }

        private void LayoutReferenceTeam(List<UnitVisual> units, bool enemy)
        {
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                int column = units.Count == 1 ? 1 : units.Count == 2 ? i * 2 : (i == 0 ? 1 : i == 1 ? 0 : i % 3);
                int row = i / 3;
                float x = 175 + column * 295;
                float y = (enemy ? 685 : 1238) + (column == 1 ? -25 : 18) - row * 125;
                float modelHeight = (enemy ? 275 : units.Count == 1 ? 430 : 330) / Mathf.Max(1, 1 + row * .25f);
                if (_wideBattleLayout)
                {
                    x = 430 + column * 406;
                    y = (enemy ? 370 : 699) + (column == 1 ? -20 : 15) - row * 80;
                    modelHeight = (enemy ? 190 : 270) / Mathf.Max(1, 1 + row * .25f);
                }
                Vector3 home = ReferenceWorld(x, y);
                float height = Vector3.Distance(home, ReferenceWorld(x, y - modelHeight));
                unit.Home = home;
                unit.Sphere.position = home;
                unit.BaseScale = Vector3.one * (height / CharacterNormalizedHeight);
                unit.Sphere.localScale = unit.BaseScale;
                unit.Sphere.rotation = _battleCamera.transform.rotation;
                UpdateInkCharacterSprite(unit);
                CreateReferenceUnitHud(unit);
                if (_referenceUnitHuds.TryGetValue(unit.UnitId, out var hud))
                {
                    hud.Root.localScale = Vector3.one;
                    hud.Name.text = ReferenceUnitName(unit.UnitId);
                }
            }
        }

        private void RefreshReferenceViewport()
        {
            if (_referenceDesign == null || _battleCamera == null) return;
            var viewport = new Vector2(Screen.width, Screen.height);
            if (viewport == _referenceViewport && _referenceScale == _referenceDesign.lossyScale &&
                _referencePosition == _referenceDesign.anchoredPosition) return;
            _referenceViewport = viewport;
            _referenceScale = _referenceDesign.lossyScale;
            _referencePosition = _referenceDesign.anchoredPosition;
            FitBattleBackground();
            LayoutReferenceFormation();
        }
    }
}
