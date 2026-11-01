using UnityEngine;

namespace XiuXian.Core
{
    [ExecuteAlways]
    public sealed class LobbyPortraitLayout : MonoBehaviour
    {
        private void LateUpdate()
        {
            var rect = (RectTransform)transform;
            var parent = rect.parent as RectTransform;
            if (parent == null || Screen.width == 0 || Screen.height == 0) return;
            Rect safe = Screen.safeArea;
            float width = parent.rect.width * safe.width / Screen.width;
            float height = parent.rect.height * safe.height / Screen.height;
            float scale = Mathf.Min(width / 941f, height / 1672f);
            rect.localScale = Vector3.one * Mathf.Max(.01f, scale);
            rect.anchoredPosition = new Vector2(
                (safe.center.x / Screen.width - .5f) * parent.rect.width,
                (safe.center.y / Screen.height - .5f) * parent.rect.height);
        }
    }
}
