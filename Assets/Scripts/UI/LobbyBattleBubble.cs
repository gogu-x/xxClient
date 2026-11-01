using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Q = XiuXian.Core.QuietInkUI;

namespace XiuXian.Core
{
    /// <summary>A lobby shortcut to the retained battle, never a new-battle request.</summary>
    public sealed class LobbyBattleBubble : MonoBehaviour
    {
        private Button button;
        private TMP_Text caption;
        private LobbyPortraitMask stateDot;
        private float nextRefresh;
        private bool lastFinished;

        public void Attach(RectTransform design)
        {
            if (design == null) return;
            if (button == null || button.transform.parent != design)
            {
                var old = design.Find("BattleResumeBubble");
                if (old != null)
                {
                    old.gameObject.SetActive(false);
                    Destroy(old.gameObject);
                }
                button = Q.Button(design, "BattleResumeBubble", "", 18, 525, 138, 158,
                    ResumeBattle, null);
                button.transition = Selectable.Transition.None;
                Circle(button.transform, "Rim", 10, 0, 118, Q.Jade);
                Circle(button.transform, "Paper", 13, 3, 112, new Color32(246, 242, 231, 248));
                Q.Icon(button.transform, "sword", 40, 24, 58, 68);
                Circle(button.transform, "DotRim", 102, 5, 18, Q.Paper);
                stateDot = Circle(button.transform, "BattleStateDot", 105, 8, 12, Q.Jade);
                var strip = Q.Rect(button.transform, "CaptionPaper", 2, 121, 134, 35).gameObject.AddComponent<Image>();
                strip.color = new Color(Q.Paper.r, Q.Paper.g, Q.Paper.b, .94f);
                strip.raycastTarget = false;
                caption = Q.Text(button.transform, "BattleState", "回到战斗", 2, 120, 134, 37, 27, Q.Jade);
                button.gameObject.SetActive(false);
            }
            RefreshState(true);
        }

        private static LobbyPortraitMask Circle(Transform parent, string name, float x, float y, float size, Color color)
        {
            var circle = Q.Rect(parent, name, x, y, size, size).gameObject.AddComponent<LobbyPortraitMask>();
            circle.color = color;
            circle.raycastTarget = false;
            return circle;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .2f;
            RefreshState(false);
        }

        private void RefreshState(bool force)
        {
            if (button == null) return;
            bool visible = BattleView.HasResumableBattle && BattleView.IsBattleMinimized;
            bool finished = BattleView.BattleIsFinished;
            if (button.gameObject.activeSelf != visible) button.gameObject.SetActive(visible);
            if (!force && lastFinished == finished) return;
            lastFinished = finished;
            caption.text = finished ? "查看结果" : "回到战斗";
            stateDot.color = finished ? Q.Gold : Q.Jade;
        }

        private void ResumeBattle()
        {
            BattleView.ResumeExisting();
            RefreshState(true);
        }
    }
}
