using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Q = XiuXian.Core.QuietInkUI;

namespace XiuXian.Core
{
    public sealed partial class LobbyFeaturePanel
    {
        private TMP_Text cultivationRealm, cultivationNext, cultivationValue, cultivationPercent;
        private TMP_Text cultivationRate, cultivationStatus, cultivationFeedback;
        private TMP_Text[] pillStocks, pillGains;
        private Button[] pillButtons;
        private Button breakthroughButton;
        private Image cultivationFill, cultivationAura;
        private RectTransform meditationArt;
        private float nextCultivationRefresh, displayedCultivationProgress, targetCultivationProgress;
        private float cultivationFlashUntil;
        private bool cultivationReady;
        private static Sprite[] cultivationPillSprites;

        private void BuildCultivation(Transform d)
        {
            Q.Text(d, "MeditationCaption", "凝 神 · 聚 气", 270, 160, 401, 42, 26, Q.Muted);
            cultivationRealm = Q.Text(d, "CultivationRealm", "", 130, 210, 681, 70, 48, Q.Ink);
            cultivationNext = Q.Text(d, "NextRealm", "", 130, 285, 681, 40, 25, Q.Muted);

            cultivationAura = Q.Art(d, "fx-ring", 240, 375, 461, 380, true);
            cultivationAura.name = "QuietCultivationAura";
            cultivationAura.color = new Color(.35f, .72f, .65f, .16f);
            var hero = Q.Rect(d, "MeditationIllustration", 180, 320, 581, 470).gameObject.AddComponent<Image>();
            hero.sprite = Resources.Load<Sprite>("UI/QuietInkV3/Cultivation/meditation");
            hero.preserveAspect = true; hero.raycastTarget = false;
            hero.enabled = hero.sprite != null;
            meditationArt = hero.rectTransform;

            var progress = CompactInfoCard.Create(d, "CultivationProgressCard", 55, 790, 831, 230).transform;
            Q.Text(progress, "ProgressTitle", "修 为", 30, 20, 220, 45, 32, Q.Ink, TextAlignmentOptions.MidlineLeft);
            cultivationPercent = Q.Text(progress, "ProgressPercent", "", 580, 22, 220, 42, 30, Q.Jade, TextAlignmentOptions.MidlineRight);
            var track = Q.Rect(progress, "ProgressTrack", 32, 88, 767, 14).gameObject.AddComponent<Image>();
            track.raycastTarget = false; track.color = new Color32(222, 225, 213, 255);
            cultivationFill = Q.Rect(track.transform, "ProgressFill", 0, 0, 0, 14).gameObject.AddComponent<Image>();
            cultivationFill.color = Q.Jade; cultivationFill.raycastTarget = false;
            cultivationValue = Q.Text(progress, "ProgressValue", "", 30, 118, 400, 38, 28, Q.Ink, TextAlignmentOptions.MidlineLeft);
            cultivationStatus = Q.Text(progress, "ProgressState", "", 450, 118, 350, 38, 26, Q.Muted, TextAlignmentOptions.MidlineRight);
            cultivationRate = Q.Text(progress, "CultivationRate", "", 30, 176, 770, 34, 24, Q.Muted, TextAlignmentOptions.MidlineLeft);

            breakthroughButton = Q.Button(d, "CultivationBreakthrough", "修炼中", 240, 1045, 461, 86, BreakthroughCultivation, "button_primary", 32);
            cultivationFeedback = Q.Text(d, "CultivationFeedback", "修为圆满，即可突破下一重境界", 80, 1142, 781, 40, 25, Q.Muted);
            Q.Text(d, "PillsTitle", "丹 药", 65, 1202, 260, 45, 34, Q.Ink, TextAlignmentOptions.MidlineLeft);
            Q.Text(d, "PillsHint", "服用后立即增长修为", 360, 1208, 516, 36, 24, Q.Muted, TextAlignmentOptions.MidlineRight);

            pillStocks = new TMP_Text[3]; pillGains = new TMP_Text[3]; pillButtons = new Button[3];
            cultivationPillSprites = Resources.LoadAll<Sprite>("UI/QuietInkV3/Cultivation/pills");
            var snapshot = CultivationProgress.GetSnapshot();
            for (int i = 0; i < 3; i++)
            {
                int pillId = i + 1;
                var pill = snapshot.Pills[i];
                var card = CompactInfoCard.Create(d, "CultivationPill_" + pillId, 55 + i * 283, 1265, 265, 324).transform;
                var icon = Q.Rect(card, "PillIcon", 77, 14, 110, 110).gameObject.AddComponent<Image>();
                icon.sprite = FindCultivationPillSprite(pillId); icon.preserveAspect = true; icon.raycastTarget = false;
                icon.enabled = icon.sprite != null;
                pillStocks[i] = Q.Text(card, "Stock", "", 175, 12, 72, 32, 23, Q.Muted, TextAlignmentOptions.MidlineRight);
                Q.Text(card, "PillName", pill.Name, 15, 126, 235, 40, 30, Q.Ink);
                pillGains[i] = Q.Text(card, "PillGain", "", 10, 170, 245, 34, 24, Q.Jade);
                pillButtons[i] = Q.Button(card, "UsePill_" + pillId, "服用", 20, 211, 225, 104, () => UseCultivationPill(pillId), "button_secondary", 28);
            }
            Q.Text(d, "LocalCultivationSave", "本机修炼 · 进度与丹药自动保存", 100, 1607, 741, 38, 23, Q.Muted);
            cultivationReady = true;
            displayedCultivationProgress = targetCultivationProgress = snapshot.Progress01;
            cultivationFlashUntil = 0;
            RefreshCultivation();
        }

        private static Sprite FindCultivationPillSprite(int id)
        {
            if (cultivationPillSprites == null) return null;
            foreach (var sprite in cultivationPillSprites)
                if (sprite.name == "cultivation_pill_" + id) return sprite;
            return null;
        }

        private void Update()
        {
            if (!cultivationReady || current != Feature.Cultivation || page == null || !page.activeInHierarchy) return;
            if (Time.unscaledTime >= nextCultivationRefresh) RefreshCultivation();
            displayedCultivationProgress = Mathf.MoveTowards(displayedCultivationProgress, targetCultivationProgress, Time.unscaledDeltaTime * 1.8f);
            if (cultivationFill != null) cultivationFill.rectTransform.sizeDelta = new Vector2(767 * displayedCultivationProgress, 14);
            float breathe = Mathf.Sin(Time.unscaledTime * 1.5f);
            if (meditationArt != null) meditationArt.anchoredPosition = new Vector2(180, -320 + breathe * 2);
            if (cultivationAura != null)
            {
                float flash = Mathf.Clamp01((cultivationFlashUntil - Time.unscaledTime) / 1.3f);
                cultivationAura.color = new Color(.35f, .72f, .65f, .13f + .035f * breathe + flash * .24f);
            }
        }

        private void RefreshCultivation()
        {
            if (cultivationRealm == null) return;
            var state = CultivationProgress.GetSnapshot();
            cultivationRealm.text = state.RealmName;
            cultivationNext.text = state.IsMaxRank ? "道心通明 · 自在逍遥" : "下一境  " + state.NextRealmName;
            targetCultivationProgress = state.Progress01;
            cultivationPercent.text = state.IsMaxRank ? "圆满" : (state.CanBreakthrough ? "100" : Math.Min(99, Math.Floor(state.Progress01 * 100)).ToString("0")) + "%";
            cultivationValue.text = state.IsMaxRank ? "修行圆满" : Math.Floor(state.Progress).ToString("N0") + " / " + state.RequiredProgress.ToString("N0");
            cultivationStatus.text = state.IsMaxRank ? "已达当前最高境界" : state.CanBreakthrough ? "灵气充盈 · 可突破" : "正在凝聚灵气";
            cultivationStatus.color = state.CanBreakthrough ? Q.Gold : Q.Muted;
            cultivationFill.color = state.CanBreakthrough || state.IsMaxRank ? Q.Gold : Q.Jade;
            if (state.IsMaxRank) cultivationRate.text = "当前境界已圆满";
            else if (state.CanBreakthrough) cultivationRate.text = "突破后继续积累修为";
            else
            {
                double minutes = (state.RequiredProgress - state.Progress) / Math.Max(1, state.GainPerMinute);
                string duration = minutes < 60 ? Math.Ceiling(minutes) + "分钟" : Math.Ceiling(minutes / 60) + "小时";
                cultivationRate.text = "自然修炼  +" + state.GainPerMinute.ToString("N0") + "/分钟    ·    距圆满约" + duration;
            }
            breakthroughButton.interactable = state.CanBreakthrough;
            breakthroughButton.GetComponentInChildren<TMP_Text>().text = state.IsMaxRank ? "境界圆满" : state.CanBreakthrough ? "突破境界" : "修炼中 · 待圆满";
            for (int i = 0; i < pillButtons.Length; i++)
            {
                var pill = state.Pills[i];
                pillStocks[i].text = "×" + pill.Count;
                pillGains[i].text = "修为 +" + pill.Gain.ToString("N0");
                bool usable = pill.Count > 0 && !state.CanBreakthrough && !state.IsMaxRank;
                pillButtons[i].interactable = usable;
                pillButtons[i].GetComponentInChildren<TMP_Text>().text = pill.Count == 0 ? "已用尽" : state.IsMaxRank || state.CanBreakthrough ? "已圆满" : "服用";
            }
            nextCultivationRefresh = Time.unscaledTime + 1f;
        }

        private void UseCultivationPill(int id)
        {
            bool success = CultivationProgress.TryUsePill(id, out string message);
            cultivationFeedback.text = message;
            cultivationFeedback.color = success ? Q.Jade : Q.Cinnabar;
            if (success) cultivationFlashUntil = Time.unscaledTime + 1.3f;
            RefreshCultivation();
        }

        private void BreakthroughCultivation()
        {
            bool success = CultivationProgress.TryBreakthrough(out string message);
            cultivationFeedback.text = message;
            cultivationFeedback.color = success ? Q.Gold : Q.Cinnabar;
            if (success)
            {
                displayedCultivationProgress = 0;
                cultivationFlashUntil = Time.unscaledTime + 1.3f;
            }
            RefreshCultivation();
        }

        private void SaveCultivation()
        {
            if (cultivationReady) CultivationProgress.Save();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveCultivation();
        }

        private void OnApplicationQuit() { SaveCultivation(); }
    }
}
