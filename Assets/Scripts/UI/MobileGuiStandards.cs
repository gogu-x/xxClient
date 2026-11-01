using UnityEngine;

namespace XiuXian.Core
{
    /// <summary>Shared sizing and safe-area rules for portrait mobile pages.</summary>
    public static class MobileGuiStandards
    {
        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;
        public const float MinimumTouchTarget = 88f;
        public const float PageSideMargin = 48f;
        public const float ModuleGap = 24f;

        public static void EnsureTouchTarget(RectTransform rect, float minimum = MinimumTouchTarget)
        {
            if (rect == null) return;
            Vector2 size = rect.sizeDelta;
            if (size.x < minimum) size.x = minimum;
            if (size.y < minimum) size.y = minimum;
            rect.sizeDelta = size;
        }

        public static float GetSafeTopInset(RectTransform page)
        {
            if (page == null || Screen.width <= 0 || Screen.height <= 0) return 0f;

            Canvas canvas = page.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            Vector2 screenPoint = new Vector2(Screen.width * 0.5f, Screen.safeArea.yMax);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(page, screenPoint, camera, out Vector2 local))
                return 0f;

            return Mathf.Max(0f, page.rect.yMax - local.y);
        }

        public static float GetSafeBottomInset(RectTransform page)
        {
            if (page == null || Screen.width <= 0 || Screen.height <= 0) return 0f;

            Canvas canvas = page.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            Vector2 screenPoint = new Vector2(Screen.width * 0.5f, Screen.safeArea.yMin);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(page, screenPoint, camera, out Vector2 local))
                return 0f;

            return Mathf.Max(0f, local.y - page.rect.yMin);
        }

public static void ApplySafeInsets(RectTransform page,
            RectTransform[] topAnchored, RectTransform[] bottomAnchored)
        {
            if (page == null) return;
            float top = GetSafeTopInset(page);
            float bottom = GetSafeBottomInset(page);

            if (topAnchored != null)
            {
                foreach (RectTransform element in topAnchored)
                {
                    if (element == null || element.anchorMin.y < 0.99f || element.anchorMax.y < 0.99f) continue;
                    Vector2 position = element.anchoredPosition;
                    position.y -= top;
                    element.anchoredPosition = position;
                }
            }

            if (bottomAnchored != null)
            {
                foreach (RectTransform element in bottomAnchored)
                {
                    if (element == null || element.anchorMin.y > 0.01f || element.anchorMax.y > 0.01f) continue;
                    Vector2 position = element.anchoredPosition;
                    position.y += bottom;
                    element.anchoredPosition = position;
                }
            }
        }
    }

}
