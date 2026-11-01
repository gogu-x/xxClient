using System.Collections.Generic;
using UnityEngine;

namespace XiuXian.Core
{
    public sealed partial class BattleView
    {
        private readonly Dictionary<RectTransform, Rect> _portraitBattleHud = new();
        private bool _wideBattleLayout;

        private void RememberBattleHudLayout()
        {
            foreach (RectTransform rect in _referenceDesign)
                _portraitBattleHud[rect] = new Rect(rect.anchoredPosition.x, -rect.anchoredPosition.y,
                    rect.sizeDelta.x, rect.sizeDelta.y);
        }

        private void UpdateBattleDesignLayout()
        {
            if (_referenceDesign == null || !(_referenceDesign.parent is RectTransform parent)) return;
            Rect safe = Screen.safeArea;
            float width = parent.rect.width * safe.width / Mathf.Max(1, Screen.width);
            float height = parent.rect.height * safe.height / Mathf.Max(1, Screen.height);
            bool wide = width > height * 1.15f;
            var designSize = wide ? new Vector2(1672, 941) : new Vector2(941, 1672);
            _referenceDesign.sizeDelta = designSize;
            _referenceDesign.localScale = Vector3.one * Mathf.Max(.01f, Mathf.Min(width / designSize.x, height / designSize.y));
            _referenceDesign.anchoredPosition = new Vector2(
                (safe.center.x / Mathf.Max(1, Screen.width) - .5f) * parent.rect.width,
                (safe.center.y / Mathf.Max(1, Screen.height) - .5f) * parent.rect.height);
            if (_wideBattleLayout == wide) return;
            _wideBattleLayout = wide;
            foreach (var pair in _portraitBattleHud)
            {
                var rect = pair.Key;
                if (rect == null) continue;
                Rect r = pair.Value;
                rect.localScale = Vector3.one;
                if (wide)
                {
                    switch (rect.name)
                    {
                        case "ExitBattle": r = new Rect(24, 24, 88, 88); break;
                        case "StageTitle": r = new Rect(575, 24, 521, 66); break;
                        case "Timeline": r = new Rect(635, 80, 401, 44); break;
                        case "Environment": r = new Rect(635, 137, 401, 32); break;
                        case "CommandPaper": r = new Rect(0, 847, 1672, 94); break;
                        case "BattleLog": r = new Rect(420, 857, 830, 72); break;
                    }
                    if (rect.name.StartsWith("SkillSlot"))
                    {
                        int slot = int.Parse(rect.name.Substring("SkillSlot".Length));
                        r.x = 498 + slot * 188;
                        r.y = 722;
                        rect.localScale = Vector3.one * .78f;
                    }
                }
                rect.anchoredPosition = new Vector2(r.x, -r.y);
                rect.sizeDelta = r.size;
            }
            LayoutBattleRecoveryControl();
            LayoutReferenceFormation();
        }

        private void LayoutBattleRecoveryControl()
        {
            if (_battleStatusText != null)
            {
                var size = _battleStatusText.rectTransform.sizeDelta;
                size.x = _wideBattleLayout ? 830 : _battleRecoveryControl != null ? 508 : 825;
                _battleStatusText.rectTransform.sizeDelta = size;
            }
            if (_battleRecoveryControl != null)
            {
                var rect = (RectTransform)_battleRecoveryControl.transform;
                rect.anchoredPosition = _wideBattleLayout
                    ? new Vector2(1298, -856) : new Vector2(598, -1586);
                rect.sizeDelta = _wideBattleLayout ? new Vector2(320, 76) : new Vector2(289, 76);
            }
        }
    }
}
